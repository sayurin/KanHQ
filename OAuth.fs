namespace Sayuri
open System
open System.Net
open System.Security.Cryptography
open System.Text

type ParameterType =
    | ProtocolString of string
    | QueryString of string
    | BodyString of string
    | BodyBinary of byte[]

[<AbstractClass>]
type OAuthBase (clientCredentials : NetworkCredential) =
    static let epoch = DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)
    static let urlEncode source =
        if String.IsNullOrEmpty source then source else
        let result = ResizeArray()
        let encodeNibble n =
            char (n + if n < 10 then int '0' else int 'A' - 10) |> result.Add
        let encodeByte b =
            if '0'B <= b && b <= '9'B || 'A'B <= b && b <= 'Z'B || 'a'B <= b && b <= 'z'B || b = '-'B || b = '.'B || b = '_'B || b = '~'B then
                char b |> result.Add
            else
                result.Add '%'
                int b / 16 |> encodeNibble
                int b % 16 |> encodeNibble
        if String.forall (fun c -> c < '\x80') source then
            String.iter (byte >> encodeByte) source
        else
            Encoding.UTF8.GetBytes source |> Array.iter encodeByte
        String(result.ToArray())
    do
        if clientCredentials = null then nullArg "consumerCredential"

    let authenticate (httpMethod : string, url, tokenCredentials : NetworkCredential, parameters) =
        if httpMethod = null then nullArg "httpMethod"
        if url = null then nullArg "url"
        if parameters = null then nullArg "parameters"

        // 3.1.  Making Requests
        let parameters = [
            yield "oauth_consumer_key", clientCredentials.UserName
            if tokenCredentials <> null then
                yield "oauth_token", tokenCredentials.UserName
            yield "oauth_signature_method", "HMAC-SHA1"
            // 3.3.  Nonce and Timestamp
            yield "oauth_timestamp", (DateTime.UtcNow - epoch).TotalSeconds |> int |> string
            yield "oauth_nonce", Guid.NewGuid().ToString "N"
            yield "oauth_version", "1.0"        // RFC5849 3.1ではoauth_versionはOPTIONAL。しかしstream.twitter.comでは必須。
            // 3.4.1.3.1.  Parameter Sources
            match Seq.fold (fun (req, opt) ->
                function
                | n, (ProtocolString v | QueryString v) -> (n, v) :: req, opt
                | n, BodyString v -> req, Option.map (fun l -> (n, v) :: l) opt
                | _, BodyBinary _ -> req, None) ([], Some []) parameters with
            | req, Some opt -> yield! req; yield! opt
            | req, None -> yield! req
        ]
        // 3.4.1.3.2.  Parameters Normalization
        let normalizedParameters =
            parameters
            |> List.map (fun (name, value) -> urlEncode name, urlEncode value)
            |> Seq.sort
            |> Seq.map (fun (name, value) -> name + "=" + value)
            |> String.concat "&"
        // 3.4.1.  Signature Base String
        let signatureBaseString = httpMethod.ToUpperInvariant() + "&" + urlEncode url + "&" + urlEncode normalizedParameters

        // 3.4.2.  HMAC-SHA1
        let key = clientCredentials.Password + "&" + if tokenCredentials <> null then tokenCredentials.Password else ""
               |> Encoding.ASCII.GetBytes
        let text = signatureBaseString |> Encoding.ASCII.GetBytes
        let digest = use hash = new HMACSHA1(key)
                     hash.ComputeHash text
        // 3.5.1.  Authorization Header
        let authorization = ("oauth_signature", digest |> Convert.ToBase64String |> urlEncode) :: parameters
                         |> List.filter (fun (name, _) -> name.StartsWith "oauth_")
                         |> List.map (fun (name, value) -> name + "=\"" + value + "\"")
                         |> String.concat ","
        "OAuth " + authorization

    abstract member RequestToken : string
    abstract member Authorize : string
    abstract member AccessToken : string
    abstract member Callback : string
        default this.Callback = "oob"

    member this.AsyncGetResponse (httpMethod, uri, tokenCredentials, parameters) =
        async{
            let authorization = authenticate(httpMethod, uri, tokenCredentials, parameters)
            let uri =
                match Array.choose (fun (name, value) -> match value with QueryString value -> Some (name, value) | _ -> None) parameters with
                | [||] -> uri
                | array -> uri + "?" + (Array.map (fun (name, value) -> urlEncode name + "=" + urlEncode value) array |> String.concat "&")
            let request = WebRequest.Create uri :?> HttpWebRequest
            request.Method <- httpMethod
            request.Headers.Add(HttpRequestHeader.Authorization, authorization)
            let body =
                parameters
                |> Array.fold (fun list ->
                    function
                    | _, (ProtocolString _ | QueryString _) -> list
                    | name, BodyString value -> Option.map (fun list -> (name, value) :: list) list
                    | _ -> None) (Some [])
                |> function
                | Some [] -> [||]
                | Some list ->
                    request.ContentType <- "application/x-www-form-urlencoded"
                    List.map (fun (name, value) -> urlEncode name + "=" + urlEncode value) list
                    |> String.concat "&"
                    |> Encoding.ASCII.GetBytes
                | None ->
                    let boundary = Guid.NewGuid().ToString "N"
                    request.ContentType <- "multipart/form-data; boundary=" + boundary
                    [|
                        for name, value in parameters do
                            match value with
                            | ProtocolString _ | QueryString _ -> ()
                            | BodyString value ->
                                yield! sprintf "--%s\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n%s\r\n" boundary name value
                                    |> Encoding.UTF8.GetBytes
                            | BodyBinary value ->
                                yield! sprintf "--%s\r\nContent-Type: application/octet-stream\r\nContent-Disposition: form-data; name=\"%s\"\r\n\r\n" boundary name
                                    |> Encoding.ASCII.GetBytes
                                yield! value
                                yield '\r'B
                                yield '\n'B
                        yield! sprintf "--%s--\r\n" boundary |> Encoding.ASCII.GetBytes
                    |]
            if body.Length > 0 then
                request.ContentLength <- body.LongLength
                use! stream = Async.FromBeginEnd(request.BeginGetRequestStream, request.EndGetRequestStream)
                do! stream.AsyncWrite body
            return! request.AsyncGetResponse()
        }

    member this.AsyncRequestCredentials () =
        async{
            let parameters = [|
                "oauth_callback", ProtocolString this.Callback
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Post, this.RequestToken, null, parameters)
            use stream = response.GetResponseStream()
            let! body = int response.ContentLength |> stream.AsyncRead
            let result =
                (Encoding.UTF8.GetString body).Split '&'
                |> Array.map (fun pair -> let pair = pair.Split([|'='|], 2) in pair.[0], pair.[1])
                |> dict
            return NetworkCredential(result.["oauth_token"], result.["oauth_token_secret"])
        }

    member this.AsyncGetAuthorizationAddress () =
        async{
            let! credentials = this.AsyncRequestCredentials()
            return this.Authorize + "?oauth_token=" + credentials.UserName, credentials
        }

    member this.AsyncRequestToken (tokenCredentials, verifier) =
        async{
            let parameters = [|
                "oauth_verifier", ProtocolString verifier
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Post, this.AccessToken, tokenCredentials, parameters)
            use stream = response.GetResponseStream()
            let! body = int response.ContentLength |> stream.AsyncRead
            let result =
                (Encoding.UTF8.GetString body).Split '&'
                |> Array.map (fun pair -> let pair = pair.Split([|'='|], 2) in pair.[0], pair.[1])
                |> dict
            return NetworkCredential(result.["oauth_token"], result.["oauth_token_secret"])
        }
