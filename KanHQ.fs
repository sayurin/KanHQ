open System
open System.Collections.Generic
open System.ComponentModel
open System.Diagnostics
open System.Drawing
open System.Drawing.Drawing2D
open System.Drawing.Imaging
open System.Globalization
open System.IO
open System.Net
open System.Net.NetworkInformation
open System.Reflection
open System.Runtime.InteropServices
open System.Text.RegularExpressions
open System.Threading
open System.Windows.Forms
open Microsoft.Win32
open Sayuri
open Sayuri.FSharp.Local
open Sayuri.JsonSerializer
open Sayuri.Windows.Forms

[<assembly: AssemblyCompany "Haxe"; AssemblyProduct "KanHQ"; AssemblyCopyright "Copyright ©  2013,2014 はぇ～">]
#if LIGHT
[<assembly: AssemblyTitle "艦これ 司令部室Light"; AssemblyFileVersion "0.5.7.0"; AssemblyVersion "0.5.7.0">]
#else
[<assembly: AssemblyTitle "艦これ 司令部室";      AssemblyFileVersion "0.6.4.1"; AssemblyVersion "0.6.4.1">]
#endif
do
    let values = [|
        "FEATURE_BROWSER_EMULATION", 8000
        "FEATURE_GPU_RENDERING", 1
        "FEATURE_SCRIPTURL_MITIGATION", 1

        // "FEATURE_AJAX_CONNECTIONEVENTS", 1
        // "FEATURE_BLOCK_CROSS_PROTOCOL_FILE_NAVIGATION", 1
        // "FEATURE_BLOCK_INPUT_PROMPTS", 1
        // "FEATURE_BLOCK_LMZ_IMG", 1
        // "FEATURE_BLOCK_LMZ_OBJECT", 1
        // "FEATURE_BLOCK_LMZ_SCRIPT", 1
        // "FEATURE_DISABLE_TELNET_PROTOCOL", 1
        // "FEATURE_DOWNLOAD_PROMPT_META_CONTROL", 1
        // "FEATURE_ENABLE_SCRIPT_PASTE_URLACTION_IF_PROMPT", 0
        // "FEATURE_FEEDS", 1
        // "FEATURE_HTTP_USERNAME_PASSWORD_DISABLE", 1
        // "FEATURE_IFRAME_MAILTO_THRESHOLD", 1
        // "FEATURE_IVIEWOBJECTDRAW_DMLT9_WITH_GDI", 0
        // "FEATURE_LOCALMACHINE_LOCKDOWN", 1
        // "FEATURE_MIME_HANDLING", 1
        // "FEATURE_NINPUT_LEGACYMODE", 0
        // "FEATURE_RESTRICT_ABOUT_PROTOCOL_IE7", 1
        // "FEATURE_RESTRICT_ACTIVEXINSTALL", 1
        // "FEATURE_SECURITYBAND", 1
        // "FEATURE_SHIM_MSHELP_COMBINE", 0
        // "FEATURE_SHOW_APP_PROTOCOL_WARN_DIALOG", 1
        // "FEATURE_STATUS_BAR_THROTTLING", 1
        // "FEATURE_UNC_SAVEDFILECHECK", 1
        // "FEATURE_VALIDATE_NAVIGATE_URL", 1
        // "FEATURE_VIEWLINKEDWEBOC_IS_UNSAFE", 1
        // "FEATURE_WEBOC_DOCUMENT_ZOOM", 1

        // undocumented
        // "FEATURE_ALIGNED_TIMERS", 1
        // "FEATURE_ALLOW_HIGHFREQ_TIMERS", 1
    |]
    let name = Path.GetFileName Application.ExecutablePath
    use key = Registry.CurrentUser.CreateSubKey @"Software\Microsoft\Internet Explorer\Main\FeatureControl"
    for subkey, value in values do
        use key = key.CreateSubKey subkey
        key.SetValue(name, value)
    AppDomain.CurrentDomain.UnhandledException.Add(fun e ->
        let path = Path.Combine(Environment.GetFolderPath Environment.SpecialFolder.DesktopDirectory, "KanHQ.txt")
        let name = Assembly.GetExecutingAssembly().GetName()
        File.AppendAllText(path, sprintf "----\r\n%O\r\n%s: %O\r\nWin: %O\r\n.NET: %O, %dbit\r\nIE: %O\r\nflash: %O\r\n%O\r\n"
            DateTime.Now
            name.Name name.Version
            Environment.OSVersion.Version
            Environment.Version (IntPtr.Size * 8)
            (Registry.GetValue("HKEY_LOCAL_MACHINE\Software\Microsoft\Internet Explorer", "Version", "unknown"))
            (Registry.GetValue("HKEY_LOCAL_MACHINE\SOFTWARE\Macromedia\FlashPlayer", "CurrentVersion", "unknown"))
            e.ExceptionObject))
    Application.SetUnhandledExceptionMode UnhandledExceptionMode.ThrowException
    Application.EnableVisualStyles()
    Application.SetCompatibleTextRenderingDefault false

#if UNUSED
let parseHttpDate (time : string) =
    let atoi2 i =
        try
            let tens = time.[i + 0]
            let tens = if tens = ' ' then 0 else int tens - int '0'
            let ones = int time.[i + 1] - int '0'
            tens * 10 + ones
        with e ->
            invalidArg "time" "Bad String"
    let atoi4 i =
        (atoi2 i) * 100 + atoi2 (i + 2)
    let month i =
        let index = "   JanFebMarAprMayJunJulAugSepOctNovDec".IndexOf time.[i .. i + 2]
        if index % 3 <> 0 then invalidArg "time" "Bad String"
        index / 3

    // 0         1         2
    // 01234567890123456789012345       ; offset
    // Sun, 06 Nov 1994 08:49:37 GMT    ; RFC 822, updated by RFC 1123
    // Sun Nov  6 08:49:37 1994         ; ANSI C's asctime() format
    // Sunday, 06-Nov-94 08:49:37 GMT   ; RFC 850, obsoleted by RFC 1036
    //    ^ time.[3]
    //       0         1
    //       01234567890123456789       ; offset from ','
    // Sunday, 06-Nov-94 08:49:37 GMT   ; RFC 850, obsoleted by RFC 1036
    match time.[3] with
    | ',' -> DateTime(atoi4 12, month 8, atoi2 5, atoi2 17, atoi2 20, atoi2 23, DateTimeKind.Utc)
    | ' ' -> DateTime(atoi4 20, month 4, atoi2 8, atoi2 11, atoi2 14, atoi2 17, DateTimeKind.Utc)
    | _   -> let i = time.IndexOf ','
             let year = atoi2 (i+9)
             DateTime((if year < 50 then 2000 else 1900) + year, month (i+5), atoi2 (i+2), atoi2 (i+12), atoi2 (i+15), atoi2 (i+18), DateTimeKind.Utc)
#endif

let anchorTR = AnchorStyles.Top|||AnchorStyles.Right
let createForm clientWidth clientHeight title init =
    Form2.Create(Settings.Default, clientWidth, clientHeight, title, init)

let twitter = Twitter()
let mutable account = None
let asyncAuthorize context (parent : Form) =
    async{
        if Option.isSome account then () else
        let tokenKey = Settings.Default.TokenKey
        let tokenSecret = Settings.Default.TokenSecret
        if not (String.IsNullOrEmpty tokenKey) && not (String.IsNullOrEmpty tokenSecret) then
            let credentials = NetworkCredential(tokenKey, tokenSecret)
            let! name = twitter.AsyncVerify credentials
            match name with
            | Some name -> account <- Some (name, credentials)
            | None -> ()
        if Option.isSome account then () else
        let credentials = ref None
        do! Async.SwitchToContext context
        let form = createForm 306 105 "艦これ 司令部室" (fun form ->
            form.FormBorderStyle <- FormBorderStyle.FixedDialog
            form.MaximizeBox <- false
            form.MinimizeBox <- false
            form.StartPosition <- FormStartPosition.Manual
            new Label(Location = Point(13, 13), AutoSize = true, Text = "Webブラウザー上でアプリを承認し、\r\nベリファイコードを入力する必要があります") |> form.Controls.Add
            new Label(Location = Point(12, 54), Size = Size(280, 2), BorderStyle = BorderStyle.Fixed3D) |> form.Controls.Add
            new Label(Location = Point(13, 75), AutoSize = true, Text = "ベリファイコード：") |> form.Controls.Add
            let code = new TextBox(Location = Point(98, 72), Size = Size(100, 19))
            code |> form.Controls.Add
            let show = new Button(Location = Point(216, 13), Size = Size(75, 23), Text = "起動", UseVisualStyleBackColor = true)
            show.Click.Add(fun _ ->
                async{
                    let! pair = twitter.AsyncGetAuthorizationAddress()
                    credentials := snd pair |> Some
                    fst pair |> Process.Start |> ignore
                } |> Async.Start)
            show |> form.Controls.Add
            let accept = new Button(Location = Point(216, 70), Size = Size(75, 23), Text = "完了", UseVisualStyleBackColor = true)
            accept.Click.Add(fun _ ->
                let code = code.Text
                async{
                    if !credentials |> Option.isNone || String.IsNullOrEmpty code then () else
                    let! credentials = twitter.AsyncRequestToken(Option.get !credentials, code)
                    let! name = twitter.AsyncVerify credentials
                    match name with
                    | Some name ->
                        account <- Some (name, credentials)
                        Settings.Default.TokenKey <- credentials.UserName
                        Settings.Default.TokenSecret <- credentials.Password
                        Settings.Default.Save()
                        do! Async.SwitchToContext context
                        form.Close()
                    | None -> ()
                } |> Async.Start)
            accept |> form.Controls.Add)
        form.Location <- let size = parent.Size - form.Size
                         parent.Location + Size(size.Width / 2, size.Height / 2)
        form.ShowDialog() |> ignore
        do! Async.SwitchToThreadPool()
    }

let tweetWindow (parent : Form) image =
    let context = SynchronizationContext.Current
    async{
        do! asyncAuthorize context parent
        match account with
        | Some (screenName, tokenCredentials) ->
            let! charactersReservedPerMedia = twitter.AsyncCharactersReservedPerMedia tokenCredentials
            let charactersReservedPerMedia = defaultArg charactersReservedPerMedia 23

            do! Async.SwitchToContext context
            let form = createForm 425 381 "艦これ 司令部室" (fun form ->
                form.FormBorderStyle <- FormBorderStyle.FixedDialog
                form.MaximizeBox <- false
                form.MinimizeBox <- false
                form.StartPosition <- FormStartPosition.Manual
                new PictureBox(Location = Point(13, 13), Size = Size(400, 240), SizeMode = PictureBoxSizeMode.StretchImage, Image = image) |> form.Controls.Add
                new Label(Location = Point(12, 351), AutoSize = true, Text = screenName) |> form.Controls.Add
                let count = new Label(Location = Point(282, 351), Size = Size(50, 12), TextAlign = ContentAlignment.MiddleRight)
                let tweet = new TextBox2(Location = Point(13, 260), Size = Size(400, 80), Multiline = true)
                let updateCount _ =
                    count.Text <- 140 - charactersReservedPerMedia - tweet.TextLength |> string
                updateCount ()
                tweet.TextChanged.Add updateCount
                count |> form.Controls.Add
                tweet |> form.Controls.Add
                let button = new Button(Location = Point(338, 346), Size = Size(75, 23), UseVisualStyleBackColor = true, Text = "呟く")
                button.Click.Add(fun _ ->
                    button.Enabled <- false
                    let tweet = tweet.Text
                    async{
                        use stream = new MemoryStream()
                        image.Save(stream, ImageFormat.Jpeg)
                        let result = ref false
                        try
                            do! twitter.AsyncUpdateStatusWithMedia(tokenCredentials, tweet, stream.ToArray())
                            result := true
                        with _ -> ()
                        do! Async.SwitchToContext context
                        if !result then form.Close() else button.Enabled <- true
                    } |> Async.Start)
                button |> form.Controls.Add)
            form.Location <- let size = parent.Size - form.Size
                             parent.Location + Size(size.Width / 2, size.Height / 2)
            form.Show parent
        | None -> ()
    } |> Async.Start

let getCapture (webBrowser : WebBrowser) =
    WebBrowser2.GetElementById(webBrowser.Document.DomDocument, "game_frame")
    |> Option.bind (fun iframe -> WebBrowser2.GetElementById(WebBrowser2.GetFrameDocument iframe, "externalswf"))
    |> Option.map WebBrowser2.GetCapture

let getImage webBrowser =
    getCapture webBrowser |> Option.map (fun (size, capture) -> let bitmap = new Bitmap(size.Width, size.Height, PixelFormat.Format24bppRgb)
                                                                use graphics = Graphics.FromImage bitmap
                                                                graphics.GetHdc() |> capture
                                                                graphics.ReleaseHdc()
                                                                bitmap)

let saveImage webBrowser =
    getImage webBrowser |> Option.iter (fun bitmap ->
        if Control.ModifierKeys = Keys.Shift then
            use dialog = new FolderBrowserDialog(Description = "保存するフォルダーを選択します。")
            if dialog.ShowDialog() = DialogResult.OK then
                Settings.Default.PictureFolder <- dialog.SelectedPath
                Settings.Default.Save()
        async{
            let folder = Settings.Default.PictureFolder
            let folder = if Directory.Exists folder then folder else Environment.GetFolderPath Environment.SpecialFolder.MyPictures
            let folder = if Directory.Exists folder then folder else "."
            let names = DirectoryInfo(folder).GetFiles "艦これ-*.png" |> Array.map (fun fi -> fi.Name)
            let rec loop i =
                let name = sprintf "艦これ-%03d.png" i
                if Array.forall ((<>) name) names then Path.Combine(folder, name) else loop (i + 1)
            bitmap.Save(loop 1, ImageFormat.Png)
        } |> Async.Start)

let tweetImage webBrowser form =
    getImage webBrowser |> Option.iter (tweetWindow form)

let configCapture () =
    let config = Capture.configList()
    let form = createForm 486 174 "艦これ 司令部室 - 動画設定" (fun form ->
        form.Controls.Add <| new Label(Location = Point(13, 16), AutoSize = true, Text = "保存フォルダー:")
        let folder = new TextBox(Location = Point(96, 13), Size = Size(291, 19), Text = Settings.Default.VideoFolder)
        form.Controls.Add folder
        let select = new Button(Location = Point(393, 11), Size = Size(75, 23), UseVisualStyleBackColor = true, Text = "選択...")
        select.Click.Add(fun _ ->
            use dialog = new FolderBrowserDialog(Description = "保存するフォルダーを選択します。")
            if dialog.ShowDialog() = DialogResult.OK then folder.Text <- dialog.SelectedPath)
        form.Controls.Add select
        form.Controls.Add <| new Label(Location = Point(12, 49), AutoSize = true, Text = "ファイル形式:")
        let fileFormat = new ComboBox(Location = Point(96, 46), Size = Size(121, 20), DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Item1", BindingContext = BindingContext(), DataSource = config)
        form.Controls.Add fileFormat
        form.Controls.Add <| new Label(Location = Point(15, 87), AutoSize = true, Text = "映像コーデック:")
        let videoCodec = new ComboBox(Location = Point(96, 84), Size = Size(121, 20), DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Item1", BindingContext = BindingContext())
        form.Controls.Add videoCodec
        form.Controls.Add <| new Label(Location = Point(15, 112), AutoSize = true, Text = "フレームレート:")
        let videoFrameRate = new NumericUpDown(Location = Point(96, 110), Size = Size(46, 19), TextAlign = HorizontalAlignment.Right,
                                               Minimum = 1m, Maximum = 60m, Value = decimal Settings.Default.FrameRate)
        form.Controls.Add videoFrameRate
        form.Controls.Add <| new Label(Location = Point(254, 87), AutoSize = true, Text = "音声コーデック:")
        let audioCodec = new ComboBox(Location = Point(347, 84), Size = Size(121, 20), DropDownStyle = ComboBoxStyle.DropDownList, DisplayMember = "Item1", BindingContext = BindingContext())
        form.Controls.Add audioCodec
        let fileFormatChanged _ =
            let (_ : string), (videos : (string * Guid)[], audios : (string * Guid)[]) = downcast fileFormat.SelectedItem
            videoCodec.DataSource <- videos
            videoCodec.SelectedIndex <- let index = Array.tryFindIndex (fun (_, guid) -> guid.ToString() = Settings.Default.VideoCodec) videos
                                        defaultArg index 0
            audioCodec.DataSource <- audios
            audioCodec.SelectedIndex <- let index = Array.tryFindIndex (fun (_, guid) -> guid.ToString() = Settings.Default.AudioCodec) audios
                                        defaultArg index 0
        fileFormat.SelectedIndexChanged.Add fileFormatChanged
        let ok = new Button(Location = Point(318, 139), Size = Size(75, 23), UseVisualStyleBackColor = true, Text = "OK", DialogResult = DialogResult.OK)
        ok.Click.Add(fun _ ->
            Settings.Default.VideoFolder <- folder.Text
            Settings.Default.Extension <- let (extension : string), (_ : (string * Guid)[] * (string * Guid)[]) = downcast fileFormat.SelectedItem in extension
            Settings.Default.VideoCodec <- let (_ : string), (guid : Guid) = downcast videoCodec.SelectedValue in guid.ToString()
            Settings.Default.FrameRate <- int videoFrameRate.Value
            Settings.Default.AudioCodec <- let (_ : string), (guid : Guid) = downcast audioCodec.SelectedValue in guid.ToString()
            Settings.Default.Save())
        form.Controls.Add ok
        fileFormat.SelectedIndex <- let index = Array.tryFindIndex (fun (extension, _) -> String.Equals(extension, Settings.Default.Extension, StringComparison.OrdinalIgnoreCase)) config
                                    defaultArg index 0
        fileFormatChanged()
        let cancel = new Button(Location = Point(399, 139), Size = Size(75, 23), UseVisualStyleBackColor = true, Text = "Cancel", DialogResult = DialogResult.Cancel)
        form.Controls.Add cancel
        form.AcceptButton <- ok
        form.CancelButton <- cancel
        form.FormBorderStyle <- FormBorderStyle.FixedDialog
        form.MaximizeBox <- false
        form.MinimizeBox <- false)
    form.ShowDialog() = DialogResult.OK

let zoom (webBrowser : WebBrowser2) (percent : int) =
    // モニタサイズが小さいと縮小され初期化中にResizeイベントが飛ぶ模様。その際、ExecWB()が失敗しCOMExceptionを引き起こす。
    try
        webBrowser.Zoom percent |> ignore
        webBrowser.Document.Window.ScrollTo(0, 0)
    with :? COMException -> ()

#if LIGHT
let mainWindow () = createForm 864 480 "艦これ 司令部室 Light" (fun form ->
    let webBrowser = new WebBrowser2(Location = Point(0, 0), Size = Size(800, 480), Anchor = (AnchorStyles.Top|||AnchorStyles.Bottom|||AnchorStyles.Left|||AnchorStyles.Right),
                                     ScriptErrorsSuppressed = true, Url = Uri "http://www.dmm.com/netgame/social/-/gadgets/=/app_id=854854/")
    let assembly = Assembly.GetExecutingAssembly()
    let muteIcon = new Bitmap(assembly.GetManifestResourceStream "Mute.png"), new Bitmap(assembly.GetManifestResourceStream "Volume.png")
    let mute       = new Button(Location = Point(812,  12), Size = Size(40, 40), Anchor = anchorTR, UseVisualStyleBackColor = true, Image = snd muteIcon)
    let screenShot = new Button(Location = Point(812,  64), Size = Size(40, 40), Anchor = anchorTR, UseVisualStyleBackColor = true, Image = new Bitmap(assembly.GetManifestResourceStream "ScreenShot.png"))
    let tweet      = new Button(Location = Point(812, 116), Size = Size(40, 40), Anchor = anchorTR, UseVisualStyleBackColor = true, Image = new Bitmap(assembly.GetManifestResourceStream "Tweet.png"))
    let resize _ =
        let width = 100 * webBrowser.Width / 800
        let height = 100 * webBrowser.Height / 480
        min width height |> zoom webBrowser
    webBrowser.Resize.Add resize
    webBrowser.DocumentCompleted.Add(fun _ ->
        if webBrowser.Document.GetElementById "game_frame" <> null then
            webBrowser.AddStyleSheet "body{margin:0;overflow:hidden}#game_frame{position:fixed;left:50%;top:-16px;margin-left:-450px;z-index:1}"
        resize ())
    let isMute = ref false
    mute.Click.Add(fun _ -> isMute := not !isMute
                            Sayuri.Mixer.mute !isMute
                            mute.Image <- (if !isMute then fst else snd) muteIcon)
    screenShot.Click.Add(fun _ -> saveImage webBrowser)
    tweet.Click.Add(fun _ -> tweetImage webBrowser form)

    form.Controls.Add webBrowser
    form.Controls.Add mute
    form.Controls.Add screenShot
    form.Controls.Add tweet)
#else

// 初回に初期化される。lock不要。Countプロパティで要素の有無を確認すれば十分。
let masterShips = Dictionary()
let masterSlotitems = Dictionary()
let masterMissions = Dictionary()

// lock必要。
let ships = Dictionary()
let slotitems = Dictionary()

// 一括更新される。lock不要。
let mutable decks = [||]

type SortableBindingList<'T>(array : 'T[]) =
    inherit BindingList<'T>(array)
    let mutable isSorted = false
    let mutable listSortDirection = ListSortDirection.Ascending
    let mutable propertyDescriptor = null
    override this.SupportsSortingCore = true
    override this.IsSortedCore = isSorted
    override this.SortPropertyCore = propertyDescriptor
    override this.SortDirectionCore = listSortDirection
    member this.OnListChanged() =
        this.OnListChanged(ListChangedEventArgs(ListChangedType.Reset, -1))
    override this.ApplySortCore(property, direction) =
        // use Seq.sortBy, this is stable sort. TODO: reverse is not stable.
        let sorted = Seq.sortBy (fun item -> property.GetValue item :?> IComparable) array |> Array.ofSeq
        if direction <> ListSortDirection.Ascending then Array.Reverse sorted
        Array.blit sorted 0 array 0 array.Length
        isSorted <- true
        propertyDescriptor <- property
        listSortDirection <- direction
        this.OnListChanged()
    override this.RemoveSortCore() =
        isSorted <- false
        propertyDescriptor <- base.SortPropertyCore
        listSortDirection <- base.SortDirectionCore
        this.OnListChanged()
    override this.SupportsSearchingCore = true
    override this.FindCore(property, key) =
        match Seq.tryFindIndex (fun element -> property.GetValue element = key) this with
        | Some i -> i
        | None -> -1

type Result =
    | Execute
    | Fail
    | Success
    | GreatSuccess

type Mission (index : int, name : string, duration : int, flagshipLevel : int, totalLevel : int, condition : int list list, drumShips : int, drumCount : int,
              exp : int, getFuel : int, getBullet : int, getSteel : int, getBauxite : int, useFuel : int, useBullet : int) =
    static let stypes = dict [[], "任意"; [2], "駆"; [3], "軽"; [5], "重"; [7;11;16;18], "空母"; [10], "航戦"; [13;14], "潜"; [16], "水母";]
    static let missions = [|
        Mission( 1, "練習航海",               15,  1,   0, [[];           [];                                          ], 0, 0,  10,   0,  30,   0,   0, 3, 0)
        Mission( 2, "長距離練習航海",         30,  2,   0, [[];           [];           [];           [];              ], 0, 0,  15,   0, 100,  30,   0, 5, 0)
        Mission( 3, "警備任務",               20,  3,   0, [[];           [];           [];                            ], 0, 0,  30,  30,  30,  40,   0, 3, 2)
        Mission( 4, "対潜警戒任務",           50,  3,   0, [[3];          [2];          [2];                           ], 0, 0,  40,   0,  60,   0,   0, 5, 0)
        Mission( 5, "海上護衛任務",           90,  3,   0, [[3];          [2];          [2];          [];              ], 0, 0,  40, 200, 200,  20,  20, 5, 0)
        Mission( 6, "防空射撃演習",           40,  4,   0, [[];           [];           [];           [];              ], 0, 0,  50,   0,   0,   0,  80, 3, 2)
        Mission( 7, "観艦式予行",             60,  5,   0, [[];           [];           [];           [];      [];  [] ], 0, 0, 120,   0,   0,  50,  30, 5, 0)
        Mission( 8, "観艦式",                180,  6,   0, [[];           [];           [];           [];      [];  [] ], 0, 0, 140,  50, 100,  50,  50, 5, 2)
        Mission( 9, "タンカー護衛任務",      240,  3,   0, [[3];          [2];          [2];          [];              ], 0, 0,  60, 350,   0,   0,   0, 5, 0)
        Mission(10, "強行偵察任務",           90,  3,   0, [[3];          [3];          [];                            ], 0, 0,  50,   0,  50,   0,  30, 3, 0)
        Mission(11, "ボーキサイト輸送任務",  300,  6,   0, [[2];          [2];          [];           [];              ], 0, 0,  40,   0,   0,   0, 250, 5, 0)
        Mission(12, "資源輸送任務",          480,  4,   0, [[2];          [2];          [];           [];              ], 0, 0,  50,  50, 250, 200,  50, 5, 0)
        Mission(13, "鼠輸送作戦",            240,  5,   0, [[3];          [2];          [2];          [2];     [2]; [] ], 0, 0,  60, 240, 300,   0,   0, 5, 4)
        Mission(14, "包囲陸戦隊撤収作戦",    360,  6,   0, [[3];          [2];          [2];          [2];     [];  [] ], 0, 0, 100,   0, 240, 200,   0, 5, 0)
        Mission(15, "囮機動部隊支援作戦",    720,  8,   0, [[7;11;16;18]; [7;11;16;18]; [2];          [2];     [];  [] ], 0, 0, 160,   0,   0, 300, 400, 5, 4)
        Mission(16, "艦隊決戦援護作戦",      900, 10,   0, [[3];          [2];          [2];          [];      [];  [] ], 0, 0, 200, 500, 500, 200, 200, 5, 4)
        Mission(17, "敵地偵察作戦",           45, 20,   0, [[3];          [2];          [2];          [2];     [];  [] ], 0, 0,  40,  70,  70,  50,   0, 3, 4)
        Mission(18, "航空機輸送作戦",        300, 15,   0, [[7;11;16;18]; [7;11;16;18]; [7;11;16;18]; [2];     [2]; [] ], 0, 0,  60,   0,   0, 300, 100, 5, 2)
        Mission(19, "北号作戦",              360, 20,   0, [[10];         [10];         [2];          [2];     [];  [] ], 0, 0,  70, 400,   0,  50,  30, 5, 4)
        Mission(20, "潜水艦哨戒任務",        120,  1,   0, [[13;14];      [3];                                         ], 0, 0,  50,   0,   0, 150,   0, 5, 4)
        Mission(21, "北方鼠輸送作戦",        140, 15,  30, [[3];          [2];          [2];          [2];     [2];    ], 3, 3,  55, 320, 270,   0,   0, 8, 7)
        Mission(22, "艦隊演習",              180, 30,  45, [[5];          [3];          [2];          [2];     [];  [] ], 0, 0, 400,   0,  10,   0,   0, 8, 7)
        Mission(23, "航空戦艦運用演習",      240, 50, 200, [[10];         [10];         [2];          [2];     [];  [] ], 0, 0, 420,   0,  20,   0, 100, 8, 8)
        Mission(25, "通商破壊作戦",         2400, 25,   0, [[5];          [5];          [2];          [2];             ], 0, 0, 180, 900,   0, 500,   0, 5, 8)
        Mission(26, "敵母港空襲作戦",       4800, 30,   0, [[7;11;16;18]; [3];          [2];          [2];             ], 0, 0, 200,   0,   0,   0, 900, 8, 8)
        Mission(27, "潜水艦通商破壊作戦",   1200,  1,   0, [[13;14];      [13;14];                                     ], 0, 0,  60,   0,   0, 800,   0, 8, 8)
        Mission(28, "西方海域封鎖作戦",     1500, 30,   0, [[13;14];      [13;14];      [13;14];                       ], 0, 0, 140,   0,   0, 900, 350, 8, 8)
        Mission(29, "潜水艦派遣演習",       1440, 50,   0, [[13;14];      [13;14];      [13;14];                       ], 0, 0, 100,   0,   0,   0, 100, 9, 4)
        Mission(30, "潜水艦派遣作戦",       2880, 55,   0, [[13;14];      [13;14];      [13;14];      [13;14];         ], 0, 0, 150,   0,   0,   0, 100, 9, 7)
        Mission(31, "海外艦との接触",        120, 60, 200, [[13;14];      [13;14];      [13;14];      [13;14];         ], 0, 0,  50,   0,  30,   0,   0, 5, 0)
        Mission(35, "MO作戦",                420, 40,   0, [[7;11;16;18]; [7;11;16;18]; [5];          [2];     [];  [] ], 0, 0, 100,   0,   0, 240, 280, 8, 8)
        Mission(36, "水上機基地建設",        540, 30,   0, [[16];         [16];         [3];          [2];     [];  [] ], 0, 0, 100, 480,   0, 200, 200, 8, 8)
        Mission(37, "東京急行",              165, 50, 200, [[3];          [2];          [2];          [2];     [2]; [2]], 4, 4,  65,   0, 380, 270,   0, 8, 8)
        Mission(38, "東京急行(弐)",          175, 65, 240, [[2];          [2];          [2];          [2];     [2]; [] ], 4, 8,  70, 420,   0, 200,   0, 8, 8)
    |]
    static let bindingList = SortableBindingList missions
    static let mutable bindedForm = null
    static let mutable executingMissions = [||]
    static let mutable deck = [||]
    static let mutable deckIndex = 1
    static let mutable hourly = false

    let duration = float duration |> TimeSpan.FromMinutes
    let mutable fuel = getFuel
    let mutable bullet = getBullet
    let mutable steel = getSteel
    let mutable bauxite = getBauxite
    let getHourly value =
        if hourly then float value / duration.TotalHours |> int else value

    static member GetBindingList (form : Form) =
        bindedForm <- form
        bindingList
    static member UpdateIndex index =
        deckIndex <- index
        if masterShips.Count = 0 || slotitems.Count = 0 || decks.Length <= deckIndex then () else
        deck <- let shipids = get "api_ship" decks.[deckIndex] |> getArray
                lock ships (fun () -> Array.choose (getNumber >> function -1.0 -> None | id -> Some ships.[id]) shipids)
        let useFuels, useBullets, stypes = Array.map (fun ship -> let ship = masterShips.[get "api_ship_id" ship |> getNumber]
                                                                  get "api_fuel_max" ship |> getNumber, getNumber ship.["api_bull_max"], getNumber ship.["api_stype"] |> int) deck |> Array.unzip3
        let drum, daihatsu = let shipSlots = Array.map (get "api_slot" >> getArray) deck
                             lock slotitems (fun () -> Array.map (Array.choose (getNumber >> function -1.0 -> None | id -> Some slotitems.[id])) shipSlots)
                             |> Array.map (fun slots -> Array.sumBy (fun id -> if id = 75.0 then 1 else 0) slots, Array.sumBy (fun id -> if id = 68.0 then 0.05 else 0.00) slots)
                             |> Array.unzip
        let daihatsu = 1.00 + (Array.sum daihatsu |> min 0.20)
        missions |> Array.iter (fun mission -> mission.Update(useFuels, useBullets, stypes, drum, daihatsu))
        if bindedForm <> null then
            bindedForm.BeginInvoke(MethodInvoker bindingList.OnListChanged) |> ignore
    static member UpdateHourly value =
        hourly <- value
        if bindedForm <> null then
            bindedForm.BeginInvoke(MethodInvoker bindingList.OnListChanged) |> ignore
    static member UpdateDecks () =
        executingMissions <- decks |> Array.map (fun deck -> getNumber (getArray deck.["api_mission"]).[1] |> int)
        Mission.UpdateIndex deckIndex

    member val Result = Fail with get, set
    member this.Index = index
    member this.Name = name
    member this.ResultString = match this.Result with Fail -> "失敗" | Success -> "成功" | GreatSuccess -> "大成功" | Execute -> "遠征中"
    member this.Duration = sprintf "%02d:%02d" (int duration.TotalHours) duration.Minutes
    member this.Count = List.length condition
    member this.FlagshipLevel = flagshipLevel
    member val TotalLevel = if totalLevel = 0 then "-" else string totalLevel
    member val Ships = let ships = Seq.countBy id condition
                                |> Seq.map (fun (t, count) -> sprintf "%s×%d" stypes.[t] count)
                       let ships = if 0 < drumCount then [sprintf "缶×%d" drumCount] |> Seq.append ships else ships
                       String.concat "、" ships
    member this.Exp = getHourly exp
    member this.Fuel = getHourly fuel
    member this.Bullet = getHourly bullet
    member this.Steel = getHourly steel
    member this.Bauxite = getHourly bauxite
    member private this.Update (useFuels, useBullets, stypes, drum, daihatsu) =
        this.Result <- if Array.exists ((=) index) executingMissions then Execute else
                       if Array.length deck < List.length condition then Fail else
                       if Array.filter ((<) 0) drum |> Array.length < drumShips then Fail else
                       if Array.sum drum < drumCount then Fail else
                       if (getNumber deck.[0].["api_lv"] |> int) < flagshipLevel then Fail else
                       if totalLevel <> 0 && deck |> Array.sumBy (get "api_lv" >> getNumber >> int) < totalLevel then Fail else
                       (Some (ResizeArray stypes), condition)
                       ||> List.fold (fun stypes cond -> stypes |> Option.bind (fun stypes -> let index = stypes.FindIndex(fun stype -> List.isEmpty cond || List.exists ((=) stype) cond)
                                                                                              if index < 0 then None else
                                                                                              stypes.RemoveAt index
                                                                                              Some stypes))
                        |> function None -> Fail | Some _ -> Success
        fuel <- int (float getFuel * daihatsu) - Array.sumBy (fun maxFuel -> int (maxFuel * float useFuel / 10.0)) useFuels
        bullet <- int (float getBullet * daihatsu) - Array.sumBy (fun maxBullet -> int (maxBullet * float useBullet / 10.0)) useBullets
        steel <- int (float getSteel * daihatsu)
        bauxite <- int (float getBauxite * daihatsu)

let missionWindow = lazy(createForm 829 789 "艦これ 司令部室 - 遠征計画" (fun form ->
    let decks = [|
        new RadioButton(AutoSize = true, Location = Point( 13, 13), Text = "第2艦隊", UseVisualStyleBackColor = true, Checked = true)
        new RadioButton(AutoSize = true, Location = Point( 83, 13), Text = "第3艦隊", UseVisualStyleBackColor = true)
        new RadioButton(AutoSize = true, Location = Point(153, 13), Text = "第4艦隊", UseVisualStyleBackColor = true)
    |]
    decks |> Array.iteri (fun i rb -> rb.CheckedChanged.Add(fun _ -> if rb.Checked then Mission.UpdateIndex(i + 1)))
    let hourly = new CheckBox(Size = Size(48, 16), Location = Point(729, 13), Anchor = anchorTR, Text = "時給", UseVisualStyleBackColor = true)
    hourly.CheckedChanged.Add(fun _ -> Mission.UpdateHourly hourly.Checked)
    let grid = new DataGridView(Size = Size(829, 754), Location = Point(0, 35), Anchor = (AnchorStyles.Top ||| AnchorStyles.Bottom ||| AnchorStyles.Left ||| AnchorStyles.Right),
                                RowHeadersVisible = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect, AutoGenerateColumns = false, AllowUserToResizeRows = false,
                                ColumnHeadersHeightSizeMode = DataGridViewColumnHeadersHeightSizeMode.DisableResizing)
                                // , RowTemplate.Height = 21
    (grid :> ISupportInitialize).BeginInit()
    grid.DefaultCellStyle <- DataGridViewCellStyle(Alignment = DataGridViewContentAlignment.MiddleRight, WrapMode = DataGridViewTriState.False)
    let left = DataGridViewCellStyle(Alignment = DataGridViewContentAlignment.MiddleLeft)
    grid.Columns.AddRange [|
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  20, DataPropertyName = "Index",         HeaderText = "ID")                              :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width = 140, DataPropertyName = "Name",          HeaderText = "遠征名", DefaultCellStyle = left) :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  40, DataPropertyName = "Count",         HeaderText = "隻数")                            :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  40, DataPropertyName = "FlagshipLevel", HeaderText = "旗艦Lv")                          :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  40, DataPropertyName = "TotalLevel",    HeaderText = "合計Lv")                          :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width = 180, DataPropertyName = "Ships",         HeaderText = "必要艦", DefaultCellStyle = left) :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "ResultString",  HeaderText = "成否",   DefaultCellStyle = left) :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Duration",      HeaderText = "時間")                            :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Exp",           HeaderText = "経験値")                          :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Fuel",          HeaderText = "燃料")                            :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Bullet",        HeaderText = "弾薬")                            :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Steel",         HeaderText = "鋼材")                            :> DataGridViewColumn
        new DataGridViewTextBoxColumn(ReadOnly = true, Width =  50, DataPropertyName = "Bauxite",       HeaderText = "ボーキ")                          :> DataGridViewColumn
    |]
    let bindingList = Mission.GetBindingList form
    grid.DataSource <- bindingList
    grid.CellFormatting.Add(fun e ->
        e.CellStyle.ForeColor <- match bindingList.[e.RowIndex].Result with Success | GreatSuccess -> SystemColors.ControlText | Fail | Execute -> SystemColors.GrayText)
    (grid :> ISupportInitialize).EndInit()
    Array.iter form.Controls.Add decks
    form.Controls.Add hourly
    form.Controls.Add grid
    form.Closing.Add(fun e -> e.Cancel <- true; form.Hide())))

type GradationLabel () =
    inherit Label ()

    let mutable color = None

    member this.Update (text, cond) =
        color <- if   cond < 20 then Some Color.Red
                 elif cond < 30 then Some Color.Orange
                 elif cond < 50 then None
                 else                Some Color.Gold
        if this.Text <> text then
            this.Text <- text
        else
            this.OnBackColorChanged EventArgs.Empty

    override this.OnPaintBackground pevent =
        match color with
        | None       -> base.OnPaintBackground pevent
        | Some color -> use brush = new LinearGradientBrush(this.ClientRectangle, color, SystemColors.Control, LinearGradientMode.Horizontal)
                        pevent.Graphics.FillRectangle(brush, this.ClientRectangle)

let mutable quests       = [|None|]
let mutable missionTimes = Array.empty
let mutable dockTimes    = Array.empty
let mutable kousyouTimes = Array.empty
let mutable dockShips = Array.empty
let mutable maxCount = 0, 0

[<DllImport("User32.dll")>]
extern bool FlashWindow(nativeint hWnd, bool bInvert);

let mainWindow () = createForm 1141 668 "艦これ 司令部室" (fun form ->
    form.Name <- "Main"
    let questLabels = Array.init 5 (fun i -> new Label(Location = Point(980, i * 16 +  28), Anchor = anchorTR, AutoSize = true) :> Control)
    let missionLabels = Array.init 3 (fun i -> new Label(Location = Point(980, i * 16 + 132), Anchor = anchorTR, AutoSize = true) :> Control)
    let dockLabels    = Array.init 4 (fun i -> new Label(Location = Point(980, i * 16 + 204), Anchor = anchorTR, AutoSize = true) :> Control)
    let kousyouLabels = Array.init 4 (fun i -> new Label(Location = Point(980, i * 16 + 292), Anchor = anchorTR, AutoSize = true) :> Control)
    let deckLabel = new Label(Location = Point(970, 364), Anchor = anchorTR, AutoSize = true)
    let shipLabels = Array.init 6 (fun i -> new GradationLabel(Location = Point(980, i * 16 + 380), Anchor = anchorTR, AutoSize = true, MinimumSize = Size(100, 0)))
    let maxCountLabel = new Label(Location = Point(970, 484), Anchor = anchorTR, AutoSize = true)
    let clock = ref true
    Seq.concat [ missionLabels; dockLabels; kousyouLabels ] |> Seq.iter (fun c -> c.Click.Add(fun _ -> clock := not !clock))
    let deckIndex = ref 0
    shipLabels |> Array.iter (fun l -> l.Click.Add(fun _ -> if 0 < decks.Length then deckIndex := (!deckIndex + 1) % decks.Length))

    let timer = new Timer(Enabled = true, Interval = 1000)
    timer.Tick.Add(fun _ ->
        questLabels |> Array.iteri (fun i c -> c.Text <- if Array.length quests <= i then "" else
                                                         match quests.[i] with Some (_, name) -> name | None -> "未取得")
        let now = DateTime.Now
        let scope = now - TimeSpan.FromSeconds 3.0
        [ missionLabels, missionTimes; dockLabels, dockTimes; kousyouLabels, kousyouTimes ] |> List.iter (fun (controls, times) ->
            controls |> Array.iteri (fun i c ->
                c.Text <- if times.Length <= i then "" else
                          match times.[i] with
                          | None -> ""
                          | Some (dt, t) ->
                          if scope < dt && dt < now then FlashWindow(form.Handle, true) |> ignore
                          if !clock then String.Format("{0:MM/dd HH:mm} {1}", dt, t) else
                          let span = dt - now |> max TimeSpan.Zero
                          sprintf "%02d:%02d:%02d %s" (int span.TotalHours) span.Minutes span.Seconds t))
        if !deckIndex < decks.Length && 0 < masterShips.Count then
            let deck = decks.[!deckIndex]
            deckLabel.Text <- getString deck.["api_name"]
            let ships = lock ships (fun () -> getArray deck.["api_ship"] |> Array.choose (function JsonNumber shipid when shipid <> -1.0 -> Some (shipid, ships.[shipid]) | _ -> None))
            shipLabels |> Array.iteri (fun i l ->
                let name, cond = if ships.Length <= i then "", 49 else
                                 let shipid, ship = ships.[i]
                                 let cond = getNumber ship.["api_cond"] |> int
                                 let name = getString masterShips.[getNumber ship.["api_ship_id"]].["api_name"]
                                 let name = if Array.exists ((=) shipid) dockShips then name + " [入渠]" else
                                            let hp  = getNumber ship.["api_nowhp"] / getNumber ship.["api_maxhp"]
                                            if   hp <= 0.25 then name + " [大破]"
                                            elif hp <= 0.50 then name + " [中破]"
                                            elif hp <= 0.75 then name + " [小破]"
                                            else                 name
                                 let name = if Settings.Default.ShowCondition then sprintf "%2d %s" cond name else name
                                 name, cond
                l.Update(name, cond))
        maxCountLabel.Text <- let maxShip, maxSlotitem = maxCount
                              sprintf "保有数: %d / %d,  %d / %d" ships.Count maxShip slotitems.Count maxSlotitem
    )

    let webBrowser = new WebBrowser2(Location = Point(0, 0), Size = Size(960, 668), Anchor = (AnchorStyles.Top|||AnchorStyles.Bottom|||AnchorStyles.Left|||AnchorStyles.Right),
                                     ScriptErrorsSuppressed = true, Url = Uri "http://www.dmm.com/netgame/social/-/gadgets/=/app_id=854854/")
    let mute       = new CheckBox(Location = Point(970, 508), Anchor = anchorTR, Text = "消音", UseVisualStyleBackColor = true)
    let screenShot = new Button(Location = Point(970, 532), Anchor = anchorTR, Text = "画像保存")
    let resize _ =
        100 * webBrowser.Width / 960 |> zoom webBrowser
    webBrowser.Resize.Add resize
    webBrowser.Navigated.Add resize
    webBrowser.NewWindow3.Add(fun e ->
        let form = createForm 1200 668 "艦これ 司令部室 - ブラウザ" (fun form ->
            let webBrowser = new WebBrowser2(Dock = DockStyle.Fill, ScriptErrorsSuppressed = true, Url = Uri("about:blank"))
            webBrowser.Closing.Add(fun _ -> form.Close())
            form.Controls.Add webBrowser
            e.ppDisp <- webBrowser.GetApplication())
        form.Show())
    mute.CheckedChanged.Add(fun _ -> Sayuri.Mixer.mute mute.Checked)
    screenShot.Click.Add(fun _ -> saveImage webBrowser)
    let tweet = new Button(Location = Point(1051, 532), Anchor = anchorTR, Text = "呟く")
    tweet.Click.Add(fun _ -> tweetImage webBrowser form)

    let capture = new Button(Location = Point(970, 560), Anchor = anchorTR, Text = "動画保存", Enabled = Capture.test())
    capture.Click
        |> Observable.scan (fun state _ ->
            match state with
            | None ->
                getCapture webBrowser |> Option.bind (fun (size, save) ->
                    if Control.ModifierKeys = Keys.Shift && not (configCapture ()) then None else
                    let folder = Settings.Default.VideoFolder
                    let folder = if Directory.Exists folder then folder else Environment.ExpandEnvironmentVariables @"%USERPROFILE%\Videos"     // .NET 4 has Environment.SpecialFolder.MyVideos
                    let folder = if Directory.Exists folder then folder else "."
                    capture.Text <- "保存停止"
                    let stop = new ManualResetEvent(false)
                    Capture.start (folder, size, save, form, stop) |> Async.Start
                    Some stop)
            | Some stop ->
                capture.Text <- "動画保存"
                stop.Set() |> ignore
                None) None
        |> Observable.add ignore

    let clear = new Button(Location = Point(1051, 560), Anchor = anchorTR, Text = "クリア")
    clear.Click.Add(fun _ ->
        let result = MessageBox.Show("艦これ 司令部室を終了し、Internet Explorer のインターネット一時ファイル\r\n（キャッシュ）を削除します。よろしいですか？", "艦これ 司令部室",
                                     MessageBoxButtons.OKCancel, MessageBoxIcon.Exclamation, MessageBoxDefaultButton.Button2)
        if result = DialogResult.OK then
            Process.Start("rundll32", "inetcpl.cpl,ClearMyTracksByProcess 8") |> ignore
            form.Close())

    let mission = new Button(Location = Point(970, 588), Anchor = anchorTR, Text = "遠征計画")
    mission.Click.Add(fun _ ->
        let form = missionWindow.Force()
        form.Show()
        form.Activate())

    let panel = new Panel(Location = Point(0, 0), Size = Size(1200, 668), Anchor = (AnchorStyles.Top|||AnchorStyles.Bottom|||AnchorStyles.Left|||AnchorStyles.Right), MaximumSize = Size(1200, 10000))
    panel.SuspendLayout()
    panel.Controls.Add webBrowser
    panel.Controls.Add(new Label(Location = Point(970,  12), Anchor = anchorTR, AutoSize = true, Text = "任務："))
    panel.Controls.AddRange questLabels
    panel.Controls.Add(new Label(Location = Point(970, 116), Anchor = anchorTR, AutoSize = true, Text = "遠征："))
    panel.Controls.AddRange missionLabels
    panel.Controls.Add(new Label(Location = Point(970, 188), Anchor = anchorTR, AutoSize = true, Text = "入渠："))
    panel.Controls.AddRange dockLabels
    panel.Controls.Add(new Label(Location = Point(970, 276), Anchor = anchorTR, AutoSize = true, Text = "建造："))
    panel.Controls.AddRange kousyouLabels
    panel.Controls.Add deckLabel
    Array.iter panel.Controls.Add shipLabels
    panel.Controls.Add maxCountLabel
    panel.Controls.Add mute
    panel.Controls.Add screenShot
    panel.Controls.Add tweet
    panel.Controls.Add capture
    panel.Controls.Add clear
    panel.Controls.Add mission
    panel.ResumeLayout false
    panel.PerformLayout()
    form.Controls.Add panel)

let unusedPort port =
    let props = IPGlobalProperties.GetIPGlobalProperties()
    let connections = props.GetActiveTcpConnections() |> Array.map (fun info -> info.LocalEndPoint)
    let listeners = props.GetActiveTcpListeners()
    let ports = Array.append connections listeners |> Array.map (fun ep -> ep.Port)
    let rec loop port =
        if Array.forall ((<>) port) ports then port else loop (port + 1)
    loop port

open Fiddler

let parseQuery line =
    let m = Regex.Match(line, @"(?:(?:^|&)(?<key>[^&=]+)=?(?<value>[^&=]*))*$")
    let captures (key : string) =
        let captures = m.Groups.[key].Captures
        Array.init captures.Count (fun i -> Regex.Replace(captures.[i].Value.Replace('+', ' '), @"%([0-9A-Fa-f]{2})", fun m -> Int32.Parse(m.Groups.[1].Value, NumberStyles.AllowHexSpecifier) |> char |> string))
    if not m.Success then invalidArg "line" "bad format"
    Array.zip (captures "key") (captures "value") |> dict

let parseJson (oSession : Session) =
    let response = oSession.GetResponseBodyAsString()
    let offset = if response.StartsWith "svdata=" then 7 else 0
    deserialize offset response |> getObject

let epoch = DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).ToLocalTime()
let masterQuests = [| ""; "編成"; "出撃"; "演習"; "遠征"; "入渠"; "工廠"; "改装" |]
let completedQuests = ResizeArray()

let afterSessionComplete (oSession : Session) =
    try
        let toDateTime json =
            let tick = getNumber json
            if tick = 0.0 then None else
            epoch + TimeSpan.FromMilliseconds tick |> Some
        let master (dictionary : Dictionary<_, _>) array =
            Array.iter (fun entry -> let entry = getObject entry
                                     dictionary.[getNumber entry.["api_id"]] <- getString entry.["api_name"]) array
        let updateShips key json =
            lock ships (fun () -> ships.Clear()
                                  get key json |> getArray |> Array.iter (fun ship -> let ship = getObject ship in ships.Add(getNumber ship.["api_id"], ship)))
            Mission.UpdateDecks()
        let mission key json =
            decks <- get key json
                  |> getArray
                  |> Array.map getObject
            Mission.UpdateDecks()
            missionTimes <- decks.[1..]
                         |> Array.map (fun deck -> let mission = getArray deck.["api_mission"]
                                                   toDateTime mission.[2] |> Option.map (fun dateTime -> dateTime, match getNumber mission.[1] |> masterMissions.TryGetValue with
                                                                                                                   | true, name -> name
                                                                                                                   | false, _   -> "遠征中"))
        let basic data =
            let data = getObject data
            maxCount <- getNumber data.["api_max_chara"] |> int, (getNumber data.["api_max_slotitem"] |> int) + 3
        let ndock data =
            let docks = getArray data |> Array.map (fun dock -> let dock = getObject dock
                                                                toDateTime dock.["api_complete_time"] |> Option.map (fun dateTime -> dateTime, getNumber dock.["api_ship_id"]))
            dockTimes <- Array.map (Option.map (fun (dateTime, sid) -> dateTime, getString masterShips.[getNumber ships.[sid].["api_ship_id"]].["api_name"])) docks
            dockShips <- Array.choose (Option.map snd) docks
        let kdock data =
            kousyouTimes <- getArray data
                         |> Array.map (fun kousyou -> let kousyou = getObject kousyou
                                                      let shipId = getNumber kousyou.["api_created_ship_id"]
                                                      if shipId = 0.0 then None else
                                                      let name = if Settings.Default.ShowShipName then getString masterShips.[shipId].["api_name"] else ""
                                                      Some (defaultArg (toDateTime kousyou.["api_complete_time"]) DateTime.Today, name))
        let destroyship shipid =
            decks |> Array.iter (fun deck -> let shipids = getArray deck.["api_ship"]
                                             shipids |> Array.tryFindIndex (fun ship -> getNumber ship = shipid)
                                                     |> Option.iter (fun index -> for i in index .. shipids.Length - 2 do shipids.[i] <- shipids.[i + 1]
                                                                                  shipids.[shipids.Length - 1] <- JsonNumber -1.0))
            let itemids = lock ships (fun () -> let itemids = getArray ships.[shipid].["api_slot"]
                                                ships.Remove shipid |> ignore
                                                itemids)
            lock slotitems (fun () -> Array.iter (getNumber >> function -1.0 -> () | itemid -> slotitems.Remove itemid |> ignore) itemids)

        match oSession.PathAndQuery with
        | "/kcsapi/api_start2" ->
            let data = parseJson oSession |> get "api_data" |> getObject
            let array = getArray data.["api_mst_ship"]
            for item in array do
                let item = getObject item
                masterShips.Add(getNumber item.["api_id"], item)
            getArray data.["api_mst_slotitem"] |> master masterSlotitems
            getArray data.["api_mst_mission"] |> master masterMissions
        | "/kcsapi/api_req_member/get_incentive" -> // 初回と再読み込み時も
            quests <- [| None |]
        | "/kcsapi/api_get_member/basic" ->         // 起動時とあと時々
            parseJson oSession |> get "api_data" |> basic
        | "/kcsapi/api_get_member/slot_item" ->      // 起動時に装備一覧を取得している。
            let items = parseJson oSession |> get "api_data" |> getArray |> Array.map getObject
            lock slotitems (fun () -> slotitems.Clear()
                                      items |> Array.iter (fun item -> slotitems.Add(getNumber item.["api_id"], getNumber item.["api_slotitem_id"])))
        | "/kcsapi/api_get_member/questlist" ->     // 任務を操作後に最新情報を取得している。
            let data = parseJson oSession
                    |> get "api_data"
                    |> getObject
            match data.["api_list"] with
            | JsonArray array ->
                let count = getNumber data.["api_exec_count"] |> int
                let array = Array.choose (function JsonObject item -> Some (getNumber item.["api_no"], getNumber item.["api_state"], item) | _ -> None) array
                let restQuest = if Array.exists (fun (no, state, _) -> state = 1.0 && completedQuests.Contains no) array then
                                    completedQuests.Clear()
                                    None
                                else
                                    Array.iter (fun (no, state, _) -> if state = 3.0 then completedQuests.Add no) array
                                    let array = Array.map (fun (no, _, _) -> no) array
                                    Some (Array.min array, Array.max array)
                let newQuests = array |> Array.choose (fun (no, state, item) -> if state = 1.0 then None else
                                                                                Some (no, getString item.["api_title"] |> sprintf "%s; %s" masterQuests.[getNumber item.["api_category"] |> int]))
                let restQuests = match restQuest with None -> [||] | Some (min, max) -> Array.choose (function Some (no, _) when min <= no && no <= max -> None | item -> item) quests
                let sortedQuests = Array.append restQuests newQuests |> Array.sort
                quests <- Array.init count (fun i -> if i < sortedQuests.Length then Some sortedQuests.[i] else None)
            | _ -> ()
        | "/kcsapi/api_req_quest/clearitemget" ->   // 完遂した任務の報酬を受け取る。
            let completedQuestId = oSession.GetRequestBodyAsString() |> parseQuery |> get "api_quest_id" |> float
            quests <- Array.filter (function Some (id, _) -> id <> completedQuestId | None -> true) quests
        | "/kcsapi/api_get_member/deck" ->          // 遠征後に艦隊情報を取得している。
            parseJson oSession |> mission "api_data"
        | "/kcsapi/api_port/port" ->
            let data = parseJson oSession |> get "api_data" |> getObject
            updateShips "api_ship" data
            mission "api_deck_port" data
            basic data.["api_basic"]
            ndock data.["api_ndock"]
        | "/kcsapi/api_get_member/ship2" ->         // 遠征後と入渠時に艦情報の一覧を取得している。
            let json = parseJson oSession
            updateShips "api_data" json
            mission "api_data_deck" json
        | "/kcsapi/api_get_member/ndock" ->         // 起動時と入渠時に状態を取得している。艦名は含まれていない。先行する /ship か /ship2 の情報が必要
            parseJson oSession |> get "api_data" |> ndock
        | "/kcsapi/api_get_member/kdock" ->         // 建造時に状態を取得している。
            parseJson oSession |> get "api_data" |> kdock
        | "/kcsapi/api_req_kousyou/getship" ->
            let data = parseJson oSession |> get "api_data" |> getObject
            match data.TryGetValue "api_slotitem" with
            | false, _ -> ()
            | true, items -> lock slotitems (fun () -> getArray items |> Array.iter (fun item -> let item = getObject item in slotitems.Add(getNumber item.["api_id"], getNumber item.["api_slotitem_id"])))
            lock ships (fun () -> ships.Add(getNumber data.["api_id"], getObject data.["api_ship"]))
            kdock data.["api_kdock"]
        | "/kcsapi/api_req_kousyou/destroyship" ->
            oSession.GetRequestBodyAsString() |> parseQuery |> get "api_ship_id" |> float |> destroyship
            Mission.UpdateDecks()
        | "/kcsapi/api_req_kousyou/createitem" ->
            let data = parseJson oSession |> get "api_data" |> getObject
            match data.TryGetValue "api_slot_item" with
            | false, _ -> ()
            | true, item -> let item = getObject item in lock slotitems (fun () -> slotitems.Add(getNumber item.["api_id"], getNumber item.["api_slotitem_id"]))
        | "/kcsapi/api_req_kousyou/destroyitem2" ->
            let slotitemids = oSession.GetRequestBodyAsString() |> parseQuery |> get "api_slotitem_ids"
            lock slotitems (fun () -> slotitemids.Split ',' |> Array.iter (float >> slotitems.Remove >> ignore))
        | "/kcsapi/api_req_kaisou/powerup" ->
            let request = oSession.GetRequestBodyAsString() |> parseQuery
            request.["api_id_items"].Split ',' |> Array.iter (float >> destroyship)
            let data = parseJson oSession |> get "api_data" |> getObject
            lock ships (fun () -> ships.[float request.["api_id"]] <- getObject data.["api_ship"])
            mission "api_deck" data
        | "/kcsapi/api_req_hensei/change" ->
            let remove shipids index =
                for i = index to Array.length shipids - 2 do shipids.[i] <- shipids.[i + 1]
                shipids.[shipids.Length - 1] <- JsonNumber -1.0
            let request = oSession.GetRequestBodyAsString() |> parseQuery
            let shipids = getArray decks.[int request.["api_id"] - 1].["api_ship"]
            match int request.["api_ship_idx"], float request.["api_ship_id"] with
            |  -1, _    -> for i in 1 .. shipids.Length - 1 do shipids.[i] <- JsonNumber -1.0
            | idx, -1.0 -> remove shipids idx
            | idx, sid1 -> let sid2 = getNumber shipids.[idx]
                           decks |> Array.iter (fun deck -> let shipids = getArray deck.["api_ship"]
                                                            shipids |> Array.tryFindIndex (fun sid -> sid1 = getNumber sid)
                                                                    |> Option.iter (fun idx -> if sid2 = -1.0 then remove shipids idx else shipids.[idx] <- JsonNumber sid2))
                           shipids.[if 0 < idx && getNumber shipids.[idx - 1] = -1.0 then idx - 1 else idx] <- JsonNumber sid1
            Mission.UpdateDecks()
        | "/kcsapi/api_get_member/ship3" ->         // 装備変更後に取得している。
            let data = parseJson oSession |> get "api_data" |> getObject
            lock ships (fun () -> getArray data.["api_ship_data"] |> Array.iter (fun ship -> let ship = getObject ship in ships.[getNumber ship.["api_id"]] <- ship))
            mission "api_deck_data" data
        | "/kcsapi/api_req_nyukyo/start" ->         // 入渠命令
            let request = oSession.GetRequestBodyAsString() |> parseQuery
            if int request.["api_highspeed"] = 1 then
                let ship = lock ships (fun () -> ships.[float request.["api_ship_id"]])
                ship.["api_nowhp"] <- ship.["api_maxhp"]
        | _ -> ()
    with e -> Debug.WriteLine e
#endif

[<EntryPoint; STAThread>]
let main argv =
#if LIGHT
#else
    use agent = MailboxProcessor.Start(fun inbox ->
        let rec loop () = async {
            let! session = inbox.Receive()
            afterSessionComplete session
            return! loop ()
        }
        loop ())
    use __ = resource
                (fun () -> let port = unusedPort 0x344F
                           FiddlerApplication.add_AfterSessionComplete(SessionStateHandler agent.Post)
                           FiddlerApplication.Startup(port, FiddlerCoreStartupFlags.ChainToUpstreamGateway)
                           URLMonInterop.SetProxyInProcess(sprintf "127.0.0.1:%d" port, null))
                (fun () -> URLMonInterop.ResetProxyInProcessToDefault()
                           FiddlerApplication.Shutdown())
#endif

    mainWindow () |> Application.Run
    0
