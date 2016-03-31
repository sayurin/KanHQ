module Sayuri.JsonSerializer
open System
open System.Collections.Generic
open System.Globalization
open System.Text

[<CompilationRepresentation(CompilationRepresentationFlags.UseNullAsTrueValue)>]
type JsonType =
| JsonNull
| JsonBoolean of bool
| JsonNumber  of float
| JsonString  of string
| JsonArray   of JsonType[]
| JsonObject  of IDictionary<string, JsonType>

let getBoolean = function
    | JsonBoolean b -> b
    | _ -> failwith "not boolean"

let getNumber = function
    | JsonNumber d -> d
    | _ -> failwith "not number"

let getString = function
    | JsonString s -> s
    | _ -> failwith "not string"

let getArray = function
    | JsonArray a -> a
    | _ -> failwith "not array"

let getObject = function
    | JsonObject o -> o
    | _ -> failwith "not object"

let deserialize offset (s : string) =
    let i = ref offset

    let rec readNectChar () =
        let c = s.[!i]
        incr i
        if c = '\t' || c = '\n' || c = '\r' || c = ' ' then readNectChar () else c

    let parseHex () =
        let mutable result = 0
        for i = !i to !i + 3 do
            result <- result * 16 + match s.[i] with
                                    | c when '0' <= c && c <= '9' -> int c - int '0'
                                    | c when 'A' <= c && c <= 'F' -> int c - int 'A' + 10
                                    | c when 'a' <= c && c <= 'f' -> int c - int 'a' + 10
                                    | _ -> failwith ""
        i := !i + 4
        char result

    let deserializeString first =
        if first <> '"' then failwith ""
        let string = StringBuilder()
        let rec loop escaped =
            let c = s.[!i]
            incr i
            if escaped then
                (match c with
                | '"' | '\'' | '/' | '\\'
                      -> c
                | 'b' -> '\b'
                | 'f' -> '\f'
                | 'n' -> '\n'
                | 'r' -> '\r'
                | 't' -> '\t'
                | 'u' -> parseHex ()
                | _   -> failwith "") |> string.Append |> ignore
                loop false
            elif c = '\\' then
                loop true
            elif c <> '"' then
                string.Append c |> ignore
                loop false
        loop false
        string.ToString()

    let deserializeNumber () =
        let sign =
            match s.[!i] with
            | '-' -> incr i; -1.0
            | _   ->         +1.0

        let significand, exponent1 =
            let integer =
                match s.[!i] with
                | '0' -> incr i; 0.0
                | c when '1' <= c && c <= '9' ->
                    let rec loop value =
                        match byte s.[!i] with
                        | c when '0'B <= c && c <= '9'B ->
                            incr i
                            loop (value * 10.0 + float (c - '0'B))
                        | _ -> value
                    loop 0.0
                | _ -> failwith ""

            match s.[!i] with
            | '.' ->
                incr i
                let rec loop value exponent =
                    match byte s.[!i] with
                    | c when '0'B <= c && c <= '9'B ->
                        incr i
                        loop (value * 10.0 + float (c - '0'B)) (exponent - 1)
                    | _ -> value, exponent
                loop integer 0
            | _ -> integer, 0

        let exponent2 =
            match s.[!i] with
            | 'E' | 'e' ->
                incr i
                let sign =
                    match s.[!i] with
                    | '+' -> incr i; +1
                    | '-' -> incr i; -1
                    | _   ->         +1
                let rec loop value =
                    match byte s.[!i] with
                    | c when '0'B <= c && c <= '9'B ->
                        incr i
                        loop (value * 10 + int (c - '0'B))
                    | _ -> value
                sign * loop 0
            | _ -> 0

        sign * significand * 10.0 ** float (exponent1 + exponent2)

    let isMatch substr =
        let rec loop i j =
            j = String.length substr || s.[i] = substr.[j] && loop (i + 1) (j + 1)
        loop !i 0

    let rec deserialize () =
        match readNectChar () with
        | '{' ->
            let dictionary = Dictionary()
            let rec loop () =
                let key = readNectChar () |> deserializeString
                if readNectChar () <> ':' then failwith ""
                dictionary.[key] <- deserialize ()
                match readNectChar () with
                | '}' -> ()
                | ',' -> loop ()
                | _   -> failwith ""
            if s.[!i] <> '}' then loop () else incr i
            JsonObject dictionary
        | '[' ->
            if s.[!i] = ']' then incr i; JsonArray Array.empty else
            let list = ResizeArray()
            let rec loop () =
                deserialize () |> list.Add
                match readNectChar () with
                | ']' -> ()
                | ',' -> loop ()
                | _   -> failwith ""
            loop ()
            list.ToArray() |> JsonArray
        | '"' -> deserializeString '\"' |> JsonString
        | 'n' when isMatch "ull"  -> i := !i + 3; JsonNull
        | 't' when isMatch "rue"  -> i := !i + 3; JsonBoolean true
        | 'f' when isMatch "alse" -> i := !i + 4; JsonBoolean false
        | _ -> decr i; deserializeNumber () |> JsonNumber

    deserialize ()

let serialize json =
    let sb = StringBuilder()

    let serializeString s =
        ignore <| sb.Append '"'
        let l = String.length s
        let mutable i = 0
        while i < l do
            let o = i
            while i < l && let c = s.[i] in '\x20' <= c &&  c <> '"' && c <> '\\' do
                i <- i + 1
            if o < i then
                ignore <| sb.Append(s, o, i - o)
            if i < l then
                ignore <| sb.AppendFormat(CultureInfo.InvariantCulture, "\\u{0:X4}", int s.[i])
                i <- i + 1
        sb.Append '"'

    let rec serialize = function
        | JsonNull      -> sb.Append "null"
        | JsonBoolean b -> sb.Append(if b then "true" else "false")
        | JsonNumber n  -> sb.AppendFormat(CultureInfo.InvariantCulture, "{0:g}", n)
        | JsonString s  -> serializeString s
        | JsonArray a   -> ignore <| sb.Append '['
                           for i in 0 .. Array.length a - 1 do
                               if i <> 0 then ignore <| sb.Append ','
                               ignore <| serialize a.[i]
                           sb.Append ']'
        | JsonObject o  -> ignore <| sb.Append '{'
                           let mutable first = true
                           for p in o do
                               if first then first <- false else ignore <| sb.Append ','
                               ignore <| serializeString p.Key
                               ignore <| sb.Append ':'
                               ignore <| serialize p.Value
                           sb.Append '}'

    (serialize json).ToString()
