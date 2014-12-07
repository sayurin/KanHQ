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

// stable sort based on FSharp.Core/local.fs
module Array =
    // stable sort use LanguagePrimitives.FastGenericComparerCanBeNull, but it isn't public. (based on FSharp.Core/prim-types.fs)
    module private LanguagePrimitives =
        type FastGenericComparerTable<'T when 'T : comparison>() =
            static let fCanBeNull = 
                match Type.GetTypeCode typeof<'T> with 
                | TypeCode.Byte
                | TypeCode.Char
                | TypeCode.SByte
                | TypeCode.Int16
                | TypeCode.Int32
                | TypeCode.Int64
                | TypeCode.UInt16
                | TypeCode.UInt32
                | TypeCode.UInt64
                | TypeCode.Double
                | TypeCode.Single
                | TypeCode.Decimal -> null
                // TODO: DateTime should be null?
                // | TypeCode.DateTime -> null
                // | TypeCode.String -> unboxPrim (box StringComparer)
                | _ ->
                    // let ty = typeof<'T>
                    // if   ty.Equals(typeof<nativeint>)  then unboxPrim (box IntPtrComparer)
                    // elif ty.Equals(typeof<unativeint>) then unboxPrim (box UIntPtrComparer)
                    // else
                    LanguagePrimitives.FastGenericComparer<'T>

            static member ValueCanBeNullIfDefaultSemantics = fCanBeNull

        let FastGenericComparerCanBeNull<'T when 'T : comparison> = FastGenericComparerTable<'T>.ValueCanBeNullIfDefaultSemantics

    let stableSortByWithReverse projection reverse array =
        let len = Array.length array
        if len < 2 then
            downcast array.Clone()
        else
            let keys = Array.map projection array
            let places = Array.init len id
            let cFast = LanguagePrimitives.FastGenericComparerCanBeNull
            Array.Sort(keys, places, cFast)

            if reverse then
                Array.Reverse keys
                Array.Reverse places

            let c = if cFast <> null then cFast else LanguagePrimitives.FastGenericComparer
            let mutable i = 0
            while i < len do
                let mutable j = i
                let ki = keys.[i]
                while j < len && (j = i || c.Compare(ki, keys.[j]) = 0) do
                   j <- j + 1
                if j - i >= 2 then
                    Array.Sort(places, i, j-i)
                i <- j

            Array.map (Array.get array) places

    let stableSortBy projection array =
        stableSortByWithReverse projection false array

    let stableSortInPlace array =
        let len = Array.length array
        if 2 <= len then
            match LanguagePrimitives.FastGenericComparerCanBeNull with
            | null ->
                Array.Sort(array)
            | cFast ->
                let places = Array.init len id
                Array.Sort(array, places, cFast)

                let mutable i = 0
                while i < len do
                    let mutable j = i
                    let ki = array.[i]
                    while j < len && (j = i || cFast.Compare(ki, array.[j]) = 0) do
                       j <- j + 1
                    if 2 <= j - i then
                        Array.Sort(places, array, i, j-i)
                    i <- j
