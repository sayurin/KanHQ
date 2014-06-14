namespace Sayuri.IO.Compression
open System
open System.IO
open System.IO.Compression

type ZlibStream (stream : Stream, mode, leaveOpen) =
    inherit Stream ()
    [<Literal>]
    let ModuloAdler = 65521u
    do
        if stream = null then ArgumentNullException "stream" |> raise
    let deflate = lazy (
        if mode = CompressionMode.Compress then
            stream.WriteByte 0x58uy
            stream.WriteByte 0x85uy
        else
            stream.ReadByte() |> ignore
            stream.ReadByte() |> ignore
        new DeflateStream(stream, mode, true))
    let mutable adler32 = 1u

    let updateAdler32 (buffer : byte[], offset, count) =
        let mutable s1     = adler32 &&& 0xFFFFu
        let mutable s2     = adler32 >>> 16
        let mutable count  = count
        let mutable offset = offset
#if STANDARD
        while count > 0 do
            s1     <- (s1 + uint32 buffer.[offset]) % ModuloAdler
            s2     <- (s2 + s1                    ) % ModuloAdler
            count  <- count - 1
            offset <- offset + 1
#else
        while count > 0 do
            let mutable stride = max count 5550
            count <- count - stride
            while stride > 0 do
                s1     <- s1 + uint32 buffer.[offset]
                s2     <- s2 + s1
                stride <- stride - 1
                offset <- offset + 1
            s1 <- (s1 &&& 0xFFFFu) + (s1 >>> 16) * (65536u - ModuloAdler)
            s2 <- (s2 &&& 0xFFFFu) + (s2 >>> 16) * (65536u - ModuloAdler)

        s1 <- s1 % ModuloAdler
        s2 <- (s2 &&& 0xFFFFu) + (s2 >>> 16) * (65536u - ModuloAdler)
        s2 <- s2 % ModuloAdler
#endif
        adler32 <- s2 <<< 16 ||| s1

    new(stream, mode) = new ZlibStream(stream, mode, false)
    member this.BaseStream = deflate.Force().BaseStream
    override this.CanRead = deflate.Force().CanRead
    override this.CanSeek = deflate.Force().CanSeek
    override this.CanWrite = deflate.Force().CanWrite
    override this.Length = deflate.Force().Length
    override this.Position
        with get () = deflate.Force().Position
        and set value = deflate.Force().Position <- value
    override this.BeginRead (buffer, offset, count, callback, state) =
        deflate.Force().BeginRead(buffer, offset, count, callback, state)
    override this.BeginWrite (buffer, offset, count, callback, state) =
        let result = deflate.Force().BeginWrite(buffer, offset, count, callback, state)
        updateAdler32 (buffer, offset, count)
        result
    override this.EndRead asyncResult =
        deflate.Force().EndRead asyncResult
    override this.EndWrite asyncResult =
        deflate.Force().EndWrite asyncResult
    override this.Flush () =
        deflate.Force().Flush()
    override this.Read (buffer, offset, count) =
        deflate.Force().Read(buffer, offset, count)
    override this.Seek(offset, origin) =
        deflate.Force().Seek(offset, origin)
    override this.SetLength value =
        deflate.Force().SetLength value
    override this.Write (buffer, offset, count) =
        deflate.Force().Write(buffer, offset, count)
        updateAdler32 (buffer, offset, count)
    override this.Dispose disposing =
        base.Dispose disposing
        deflate.Force().Close()
        if mode = CompressionMode.Compress then
            adler32 >>> 24 |> byte |> stream.WriteByte
            adler32 >>> 16 |> byte |> stream.WriteByte
            adler32 >>>  8 |> byte |> stream.WriteByte
            adler32 >>>  0 |> byte |> stream.WriteByte
        if not leaveOpen then
            stream.Close()
