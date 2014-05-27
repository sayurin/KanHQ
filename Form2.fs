namespace Sayuri.Windows.Forms
open System
open System.Configuration
open System.Drawing
open System.Runtime.InteropServices
open System.Windows.Forms

#nowarn "9"

[<Serializable; StructLayout(LayoutKind.Sequential); AllowNullLiteral>]
type private WINDOWPLACEMENT =
    val mutable length : int
    [<DefaultValue>]
    val mutable flags : int
    [<DefaultValue>]
    val mutable showCmd : int
    // ptMinPosition was a by-value POINT structure
    [<DefaultValue>]
    val mutable ptMinPosition_x : int
    [<DefaultValue>]
    val mutable ptMinPosition_y : int
    // ptMaxPosition was a by-value POINT structure
    [<DefaultValue>]
    val mutable ptMaxPosition_x : int
    [<DefaultValue>]
    val mutable ptMaxPosition_y : int
    // rcNormalPosition was a by-value RECT structure
    [<DefaultValue>]
    val mutable rcNormalPosition_left : int
    [<DefaultValue>]
    val mutable rcNormalPosition_top : int
    [<DefaultValue>]
    val mutable rcNormalPosition_right : int
    [<DefaultValue>]
    val mutable rcNormalPosition_bottom : int
    new () = { length = Marshal.SizeOf typeof<WINDOWPLACEMENT> }

// automatically save form position, if Form.Name defined.
type Form2 (settings : ApplicationSettingsBase) =
    inherit Form ()
 
    [<DllImport("User32.dll", CharSet = CharSet.Unicode)>]
    static extern nativeint private LoadImage(nativeint hinst, string lpszName, int uType, int cxDesired, int cyDesired, int fuLoad);
#if DEBUG
    [<DllImport("Kernel32.dll", CharSet = CharSet.Unicode)>]
    static extern nativeint private LoadLibrary(string lpFileName);
    static let GetModuleHandle lpModuleName =
        match lpModuleName with null -> Application.ExecutablePath | _ -> lpModuleName
        |> LoadLibrary
#else
    [<DllImport("Kernel32.dll", CharSet = CharSet.Unicode)>]
    static extern nativeint private GetModuleHandle(string lpModuleName);
#endif
    [<DllImport "User32.dll">]
    static extern bool private GetWindowPlacement(nativeint hWnd, WINDOWPLACEMENT placement)
    [<DllImport "User32.dll">]
    static extern bool private SetWindowPlacement(nativeint hWnd, WINDOWPLACEMENT placement)

    static let icon = lazy (LoadImage(GetModuleHandle null, "#101", (*IMAGE_ICON*)1, 0, 0, (*LR_DEFAULTCOLOR|||LR_DEFAULTSIZE*)0x00000040) |> Icon.FromHandle)

    override this.OnLoad e =
        if String.IsNullOrEmpty this.Name |> not then
            let placement = settings.[this.Name + "FormPlacement"] :?> WINDOWPLACEMENT
            if placement <> null then
                SetWindowPlacement(this.Handle, placement) |> ignore
        base.OnLoad e

    override this.OnClosing e =
        if String.IsNullOrEmpty this.Name |> not then
            let placement = WINDOWPLACEMENT()
            GetWindowPlacement(this.Handle, placement) |> ignore
            settings.[this.Name + "FormPlacement"] <- placement
            settings.Save()
        base.OnClosing e

    static member Create(settings, clientWidth, clientHeight, title, init) =
        let form = new Form2(settings, Icon = icon.Force(), Text = title)
        form.SuspendLayout()
        form.AutoScaleDimensions <- SizeF(96.0f, 96.0f)
        form.AutoScaleMode <- AutoScaleMode.Dpi
        form.ClientSize <- Size(clientWidth, clientHeight)
        init form
        form.ResumeLayout false
        form.PerformLayout()
        form
