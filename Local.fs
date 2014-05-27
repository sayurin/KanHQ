module internal Sayuri.FSharp.Local
open System
open System.Collections.Generic
open System.Threading

let inline get key (dic : IDictionary<_, _>) =
    dic.[key]

let resource ctor dtor =
    let resource = ctor ()
    { new IDisposable with member __.Dispose () = dtor resource }

// like System.Threading.CountdownEvent
type CountdownEvent (initialCount : int) =
    let mutable currentCount = initialCount
    let mutable event = new ManualResetEvent(false)

    member this.WaitHandle =
        event :> WaitHandle
    member this.AddCount () =
        Interlocked.Increment &currentCount |> ignore
    member this.Signal () =
        if Interlocked.Decrement &currentCount > 0 then false else
        event.Set() |> ignore
        true
    member this.Wait () =
        event.WaitOne() |> ignore
    interface IDisposable with
        member this.Dispose () =
            event.Close()
