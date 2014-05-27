namespace Sayuri
open System
open System.Collections.Generic
open System.IO
open System.Net
open System.Text
open Sayuri
open Sayuri.FSharp.Local
open Sayuri.JsonSerializer

type Twitter () =
    inherit OAuthBase (null)

    static let mutable configuration = None

    override this.RequestToken = "https://api.twitter.com/oauth/request_token"
    override this.Authorize = "https://api.twitter.com/oauth/authorize"
    override this.AccessToken = "https://api.twitter.com/oauth/access_token"

    member this.AsyncGetCollections (httpMethod, uri, tokenCredentials, parameters) =
        async{
            let len = Array.length parameters
            let ps = Array.zeroCreate (len + 1)
            Array.blit parameters 0 ps 0 parameters.Length
            let cursor = ref "-1"
            let results = ResizeArray()
            while !cursor <> "0" do
                ps.[len] <- "cursor", BodyString !cursor
                use! response = this.AsyncGetResponse(httpMethod, uri, tokenCredentials, ps)
                use stream = response.GetResponseStream()
                let! bytes = int response.ContentLength |> stream.AsyncRead
                let json = Encoding.UTF8.GetString bytes
                        |> deserialize 0
                        |> getObject
                results.Add json
                cursor := getString json.["next_cursor_str"]
            return results
        }

    member this.AsyncVerify tokenCredentials =
        async{
            try
                use! response = this.AsyncGetResponse(WebRequestMethods.Http.Get, "https://api.twitter.com/1.1/account/verify_credentials.json", tokenCredentials, Array.empty)
                use stream = response.GetResponseStream()
                let! bytes = int response.ContentLength |> stream.AsyncRead
                return Encoding.UTF8.GetString bytes
                    |> deserialize 0
                    |> getObject
                    |> get "screen_name"
                    |> getString
                    |> Some
            with :? WebException as e when (e.Response :?> HttpWebResponse).StatusCode = HttpStatusCode.Unauthorized ->
                e.Response.Close()
                return None
        }

    member this.AsyncGetConfiguration tokenCredentials =
        async{
            match configuration with
            | None ->
                try
                    use! response = this.AsyncGetResponse(WebRequestMethods.Http.Get, "https://api.twitter.com/1.1/help/configuration.json", tokenCredentials, Array.empty)
                    use stream = response.GetResponseStream()
                    let! bytes = int response.ContentLength |> stream.AsyncRead
                    configuration <- Encoding.UTF8.GetString bytes
                                  |> deserialize 0
                                  |> getObject
                                  |> Some
                with :? WebException as e when (e.Response :?> HttpWebResponse).StatusCode = HttpStatusCode.Unauthorized ->
                    e.Response.Close()
            | _ -> ()
        }

    member this.AsyncCharactersReservedPerMedia tokenCredentials =
        async{
            do! this.AsyncGetConfiguration tokenCredentials
            return configuration |> Option.map (fun configuration -> getNumber configuration.["characters_reserved_per_media"] |> int)
        }

    member this.AsyncGetBlockIds tokenCredentials =
        async{
            let parameters = [|
                "stringify_ids", BodyString "true";
            |]
            let! results = this.AsyncGetCollections(WebRequestMethods.Http.Get, "https://api.twitter.com/1.1/blocks/ids.json", tokenCredentials, parameters)
            return results.ConvertAll(fun result -> getArray result.["ids"])
                |> Array.concat
                |> Array.map getString
        }

    member this.AsyncSearchTweets (tokenCredentials, query) =
        async{
            let parameters = [|
                "q", BodyString query;
                "result_type", BodyString "recent";
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Get, "https://api.twitter.com/1.1/search/tweets.json", tokenCredentials, parameters)
            ()
        }

    member this.AsyncReportSpam (tokenCredentials, userId) =
        async{
            let parameters = [|
                "user_id", BodyString userId;
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Post, "https://api.twitter.com/1.1/users/report_spam.json", tokenCredentials, parameters)
            ()
        }

    member this.AsyncUpdateStatus (tokenCredentials, status) =
        async{
            let parameters = [|
                "status", BodyString status
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Post, "https://api.twitter.com/1.1/statuses/update.json", tokenCredentials, parameters)
            ()
        }

    member this.AsyncUpdateStatusWithMedia (tokenCredentials, status, media) =
        async{
            let parameters = [|
                "status", BodyString status;
                "media[]", BodyBinary media;
            |]
            use! response = this.AsyncGetResponse(WebRequestMethods.Http.Post, "https://api.twitter.com/1.1/statuses/update_with_media.json", tokenCredentials, parameters)
            ()
        }
