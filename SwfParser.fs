module Sayuri.SwfParser
open System
open System.Diagnostics
open System.Drawing
open System.Drawing.Imaging
open System.IO
open System.IO.Compression
open System.Runtime.InteropServices
open Microsoft.FSharp.NativeInterop
open Sayuri.IO.Compression

#nowarn "9"

type private PinnedPtr (array) =
    let handle = GCHandle.Alloc(array, GCHandleType.Pinned)
    member this.ToPointer () = Marshal.UnsafeAddrOfPinnedArrayElement(array, 0)
    interface IDisposable with
        member __.Dispose () = handle.Free()

type private BitReader (stream : Stream) =
    let mutable restData = 0ul
    let mutable restBits = 0

    member this.ReadUInt64 bits =
        if 64 < restBits + bits then invalidArg "bits" "too large."
        while restBits < bits do
            restData <- restData <<< restBits ||| uint32 (stream.ReadByte())
            restBits <- restBits + 8
        let result = restData >>> (restBits - bits)
        restData <- restData &&& ((1ul <<< (restBits - bits)) - 1ul)
        restBits <- restBits - bits
        result

    member this.ReadInt64 bits =
        let result = this.ReadUInt64 bits
        int64 (result <<< (64 - bits)) >>> (64 - bits)

let private toUInt16 (value : byte[]) offset =
    uint16 value.[offset + 1] <<< 8 ||| uint16 value.[offset + 0]

let private toUInt32 (value : byte[]) offset =
    uint32 value.[offset + 3] <<< 24 ||| (uint32 value.[offset + 2] <<< 16) ||| (uint32 value.[offset + 1] <<< 8) ||| uint32 value.[offset + 0]

let private readUInt16 (stream : Stream) =
    let buf = Array.zeroCreate 2
    stream.Read(buf, 0, 2) |> ignore
    toUInt16 buf 0

let private readUInt32(stream : Stream) =
    let buf = Array.zeroCreate 4
    stream.Read(buf, 0, 4) |> ignore
    toUInt32 buf 0

let private readRectangle (stream : Stream) =
    let reader = BitReader stream
    let nbits = reader.ReadUInt64 5 |> int
    let xmin = reader.ReadInt64 nbits
    let xmax = reader.ReadInt64 nbits
    let ymin = reader.ReadInt64 nbits
    let ymax = reader.ReadInt64 nbits
    xmin, xmax, ymin, ymax

let private read (stream : Stream) =
    let tagCodeAndLength = readUInt16 stream |> int
    let tag = tagCodeAndLength >>> 6
    let length = tagCodeAndLength &&& 0x3F
    let length = if length < 0x3F then length else readUInt32 stream |> int
    let contents = Array.zeroCreate length
    stream.Read(contents, 0, length) |> ignore
    tag, contents

type BitmapFormat =
    | Jpeg
    | Png
    | Gif

type Image =
    {
        CharacterID : int
        Format : BitmapFormat
        Bytes : byte[]
        Alt : Image option
    }
    static member Create(characterID, format, bytes, ?alt) = { CharacterID = characterID; Format = format; Bytes = bytes; Alt = alt }

let parse (stream : Stream) =
    let buf = Array.zeroCreate 4
    stream.Read(buf, 0, 4) |> ignore
    if (buf.[1] <> 'W'B) || (buf.[2] <> 'S'B) then invalidOp "bad data."
    let version = buf.[3]
    let fileLength = readUInt32 stream
    let stream = match buf.[0] with
                 | 'F'B -> stream
                 | 'C'B -> new ZlibStream(stream, CompressionMode.Decompress, true) :> Stream
                 | 'Z'B -> raise <| NotImplementedException("LZMA compressed SWF")
                 | _    -> invalidOp "bad data."
    let frameSize = readRectangle stream
    let frameRate = readUInt16 stream
    let frameCount = readUInt16 stream

    let rec loop () =
        match read stream with 0, _ -> [ 0, Array.empty ] | pair -> pair :: loop ()
    let tags = loop ()
    let jpegTables = List.tryPick (fun (tag, contents) -> if tag <> 8 || Array.isEmpty contents then None else Some contents) tags

    let decodeBitmap (tag, contents) =
        let decodeJpeg offset length =
            let characterID = toUInt16 contents 0 |> int
            if contents.[offset .. offset + 7] = "\x89PNG\r\n\x1A\n"B then Image.Create(characterID, Png, contents) else
            if contents.[offset .. offset + 5] = "GIF89a"B            then Image.Create(characterID, Gif, contents) else
            let mutable i = offset
            while i < length do
                if contents.[i + 0] <> 0xFFuy then failwith "bad marker."
                Debug.Write(match int contents.[i + 1] with
                            | 0xC0                          -> "SOF0 "
                            | 0xC4                          -> "DHT "
                            | 0xD8                          -> "SOI "
                            | 0xD9                          -> "EOI "
                            | 0xDB                          -> "DQT "
                            | 0xDA                          -> "SOS "
                            | x when 0xE0 <= x && x <= 0xEF -> sprintf "APP%x " (x - 0xE0)
                            | x                             -> sprintf "0x%02X " x)
                let x = int contents.[i + 1]
                if x = 0x01 || 0xD0 <= x && x <= 0xD9 then i <- i + 2 else
                i <- i + 2 + (int contents.[i + 2] <<< 16 ||| int contents.[i + 3])
                if x = 0xDA then
                    while contents.[i + 0] <> 0xFFuy || contents.[i + 0] = 0xFFuy && contents.[i + 1] = 0x00uy do
                        i <- i + 1
            Debug.WriteLine ""
            if contents.[offset + 0.. offset + 3] =  "\xFF\xD9\xFF\xD8"B then Image.Create(characterID, Jpeg, contents.[offset + 4..]) else
            if Option.isNone jpegTables                                  then Image.Create(characterID, Jpeg, contents.[offset..])     else
            failwithf "not implemented decodeJpeg: %d." characterID

        let decodeJpeg3 offset =
            let alphaDataOffset = toUInt32 contents 2 |> int
            let image = decodeJpeg offset (offset + alphaDataOffset)
            use pngStream = new MemoryStream()
            use jpegStream = new MemoryStream(image.Bytes)
            use bitmap = new Bitmap(jpegStream)
            use bitmap = bitmap.Clone(Rectangle(0, 0, bitmap.Width, bitmap.Height), PixelFormat.Format32bppArgb)
            use alphaStream = new MemoryStream(contents, offset + alphaDataOffset, contents.Length - offset - alphaDataOffset)
            use alphaStream = new ZlibStream(alphaStream, CompressionMode.Decompress)
            use alphaReader = new BinaryReader(alphaStream)
            let bitmapAlphaData = alphaReader.ReadBytes(bitmap.Width * bitmap.Height)
            let bitmapData = bitmap.LockBits(Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.ReadWrite, PixelFormat.Format32bppArgb)
            let pixelPtr = NativePtr.ofNativeInt bitmapData.Scan0
            Array.iteri (fun i -> NativePtr.set pixelPtr (i * 4 + 3)) bitmapAlphaData
            bitmap.UnlockBits bitmapData
            bitmap.Save(pngStream, ImageFormat.Png)
            Image.Create(image.CharacterID, Png, pngStream.ToArray(), image)

        let decodeLossless2 () =
            let characterID = toUInt16 contents 0 |> int
            let bitmapFormat = int contents.[2]
            let bitmapWidth  = toUInt16 contents 3 |> int
            let bitmapHeight = toUInt16 contents 5 |> int
            match bitmapFormat with
            | 3 -> let bitmapColorTableSize = int contents.[7]
                   use stream = new MemoryStream(contents.[8..])
                   use stream = new ZlibStream(stream, CompressionMode.Decompress)
                   let colorTableRGB = Array.zeroCreate (bitmapColorTableSize * 4 + 4)
                   stream.Read(colorTableRGB, 0, colorTableRGB.Length) |> ignore
                   let stride = (bitmapWidth + 3) / 4 * 4
                   let colormapPixelData = Array.zeroCreate (stride * bitmapHeight)
                   stream.Read(colormapPixelData, 0, colormapPixelData.Length) |> ignore
                   use pngStream = new MemoryStream()
                   do
                       use pinned = new PinnedPtr(colormapPixelData)
                       use bitmap = new Bitmap(bitmapWidth, bitmapHeight, stride, PixelFormat.Format8bppIndexed, pinned.ToPointer())
                       let palette = bitmap.Palette
                       for i in 0 .. bitmapColorTableSize do
                           //palette.Entries.[i] <- Color.FromArgb(int colormapPixelData.[i * 4 + 0], int colormapPixelData.[i * 4 + 1], int colormapPixelData.[i * 4 + 2], int colormapPixelData.[i * 4 + 3])
                           palette.Entries.[i] <- Color.FromArgb(int colormapPixelData.[i * 4 + 3], int colormapPixelData.[i * 4 + 0], int colormapPixelData.[i * 4 + 1], int colormapPixelData.[i * 4 + 2])
                       bitmap.Palette <- palette
                       bitmap.Save(pngStream, ImageFormat.Png)
                   Image.Create(characterID, Png, pngStream.ToArray())
            | 5 -> use stream = new MemoryStream(contents.[7..])
                   use stream = new ZlibStream(stream, CompressionMode.Decompress)
                   let bitmapPixelData  = Array.zeroCreate (bitmapWidth * bitmapHeight * 4)
                   stream.Read(bitmapPixelData, 0, bitmapPixelData.Length) |> ignore
                   use pngStream = new MemoryStream()
                   do
                       use pinned = new PinnedPtr(bitmapPixelData)
                       use bitmap = new Bitmap(bitmapWidth, bitmapHeight, bitmapWidth * 4, PixelFormat.Format32bppArgb, pinned.ToPointer())
                       bitmap.Save(pngStream, ImageFormat.Png)
                   Image.Create(characterID, Png, pngStream.ToArray())
            | _ -> failwithf "not implemented DefineBitsLossless2: %d." characterID

        match tag with
        // SWF 1
        |  6 -> // DefineBits
                decodeJpeg 2 contents.Length |> Some
        // SWF 2
        | 20 -> // DefineBitsLossless
                let characterID = toUInt16 contents 0 |> int
                Debug.WriteLine(sprintf "not implemented DefineBitsLossless: %d." characterID)
                None
        | 21 -> // DefineBitsJPEG2
                decodeJpeg 2 contents.Length |> Some
        // SWF 3
        | 35 -> // DefineBitsJPEG3
                decodeJpeg3 6 |> Some
        | 36 -> // DefineBitsLossless2
                decodeLossless2 () |> Some
        // SWF 10
        | 90 -> // DefineBitsJPEG4
                decodeJpeg3 8 |> Some
        | _  -> None

    let decodeSound (tag, contents) =
        match tag with
        | 14 -> // DefineSound
            let soundId = toUInt16 contents 0 |> int
            Debug.WriteLine(
                let b = contents.[2]
                let soundFormat = b &&& 0xF0uy >>> 4 |> int
                let soundRate = b &&& 0x0Cuy >>> 2 |> int
                let soundSize = b &&& 0x02uy >>> 1 |> int
                let soundType = b &&& 0x01uy >>> 0 |> int
                let SoundSampleCount = toUInt32 contents 3 |> int
                let soundFormat = match soundFormat with
                                  |  0 -> "Uncompressed, native-endian"
                                  |  1 -> "ADPCM"
                                  |  2 -> "MP3"
                                  |  3 -> "Uncompressed, little-endian"
                                  |  4 -> "Nellymoser 16 kHz"
                                  |  5 -> "Nellymoser 8 kHz"
                                  |  6 -> "Nellymoser"
                                  | 11 -> "Speex"
                                  | _  -> failwith "bad SoundFormat"
                let soundRate = match soundRate with
                                | 0 -> "5.5 kHz"
                                | 1 -> "11 kHz"
                                | 2 -> "22 kHz"
                                | 3 -> "44 kHz"
                                | _ -> failwith "bad SoundRate"
                sprintf "DefineSound: %d, %s, %s, %d, %d, %d" soundId soundFormat soundRate soundSize soundType SoundSampleCount)
            File.WriteAllBytes(sprintf "%d.mp3" soundId, contents.[7..])
        | _ -> ()

    //List.iter decodeBitmap tags
    //List.iter decodeSound tags
    List.choose decodeBitmap tags
