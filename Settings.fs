namespace Sayuri
open System
open System.Configuration
open System.Reflection

type Settings () =
    inherit ApplicationSettingsBase ()

    static let defaultInstance = Settings () |> SettingsBase.Synchronized :?> Settings
    static do
        let version = Assembly.GetExecutingAssembly().GetName().Version
        if (try String.IsNullOrEmpty defaultInstance.Version || Version defaultInstance.Version < version with :? ConfigurationErrorsException -> false) then
            try defaultInstance.Upgrade() with :? ConfigurationErrorsException -> defaultInstance.Reset()
            defaultInstance.Version <- version.ToString()
            defaultInstance.Save()

#if DEBUG
    static let id _ = true
#endif

    static member Default = defaultInstance

    [<UserScopedSetting; DefaultSettingValue "">]
    member this.Version
        with get () = this.["Version"] :?> string
        and set (value : string) = this.["Version"] <- value

    [<UserScopedSetting; DefaultSettingValue "">]
    member this.TokenKey
        with get () = this.["TokenKey"] :?> string
        and set (value : string) = this.["TokenKey"] <- value

    [<UserScopedSetting; DefaultSettingValue "">]
    member this.TokenSecret
        with get () = this.["TokenSecret"] :?> string
        and set (value : string) = this.["TokenSecret"] <- value 

    [<UserScopedSetting; DefaultSettingValue "">]
    member this.PictureFolder
        with get () = this.["PictureFolder"] :?> string
        and set (value : string) = this.["PictureFolder"] <- value 

    [<UserScopedSetting; SettingsSerializeAs(SettingsSerializeAs.Binary)>]
    member this.MainFormPlacement
        with get () = this.["MainFormPlacement"]
        and set (value : obj) = this.["MainFormPlacement"] <- value 

    [<UserScopedSetting; DefaultSettingValue "">]
    member this.VideoFolder
        with get () = this.["VideoFolder"] :?> string
        and set (value : string) = this.["VideoFolder"] <- value 

    [<UserScopedSetting; DefaultSettingValue "wmv">]
    member this.Extension
        with get () = this.["Extension"] :?> string
        and set (value : string) = this.["Extension"] <- value 

    [<UserScopedSetting>]
    member this.VideoFormat
        with get () = this.["VideoFormat"] :?> string
        and set (value : string) = this.["VideoFormat"] <- value 

    [<UserScopedSetting>]
    member this.VideoCodec
        with get () = this.["VideoCodec"] :?> string
        and set (value : string) = this.["VideoCodec"] <- value 

    [<UserScopedSetting; DefaultSettingValue "15">]
    member this.FrameRate
        with get () = this.["FrameRate"] :?> int
        and set (value : int) = this.["FrameRate"] <- value 

    [<UserScopedSetting>]
    member this.AudioFormat
        with get () = this.["AudioFormat"] :?> string
        and set (value : string) = this.["AudioFormat"] <- value 

    [<UserScopedSetting>]
    member this.AudioCodec
        with get () = this.["AudioCodec"] :?> string
        and set (value : string) = this.["AudioCodec"] <- value 

    [<ApplicationScopedSetting; DefaultSettingValue "False">]
    member this.ShowCondition = this.["ShowCondition"] :?> bool |> id

    [<ApplicationScopedSetting; DefaultSettingValue "False">]
    member this.ShowShipName = this.["ShowShipName"] :?> bool |> id
