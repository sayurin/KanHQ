namespace Sayuri.Windows.Forms
open System
open System.Drawing
open System.Drawing.Imaging
open System.Reflection
open System.Runtime.InteropServices
open System.Windows.Forms

#nowarn "9"

[<ComImport; Guid("6d5140c1-7436-11ce-8034-00aa006009fa"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IServiceProvider =
    abstract member QueryService : [<MarshalAs(UnmanagedType.LPStruct)>] guidService : Guid * [<MarshalAs(UnmanagedType.LPStruct)>] riid : Guid * [<Out; MarshalAs(UnmanagedType.IUnknown)>] ppvObject : obj byref -> unit

[<ComImport; Guid("0002DF05-0000-0000-C000-000000000046"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IWebBrowserApp =
    interface
    end

[<ComImport; Guid("D30C1661-CDAF-11d0-8A3E-00C04FC9E26E"); InterfaceType(ComInterfaceType.InterfaceIsDual)>]
type private IWebBrowser2 =
    // IWebBrowser members
    [<DispId( 100)>] abstract member GoBack : unit -> unit
    [<DispId( 101)>] abstract member GoForward : unit -> unit
    [<DispId( 102)>] abstract member GoHome : unit -> unit
    [<DispId( 103)>] abstract member GoSearch : unit -> unit
    [<DispId( 104)>] abstract member Navigate : [<In>] Url : string * [<In>] flags : obj byref * [<In>] targetFrameName : obj byref * [<In>] postData : obj byref * [<In>] headers : obj byref -> unit
    [<DispId(-550)>] abstract member Refresh : unit -> unit
    [<DispId( 105)>] abstract member Refresh2 : [<In>] level : obj byref -> unit
    [<DispId( 106)>] abstract member Stop : unit -> unit
    [<DispId( 200)>] abstract member Application : [<return: MarshalAs(UnmanagedType.IDispatch)>] obj
    [<DispId( 201)>] abstract member Parent : [<return: MarshalAs(UnmanagedType.IDispatch)>] obj
    [<DispId( 202)>] abstract member Container : [<return: MarshalAs(UnmanagedType.IDispatch)>] obj
    [<DispId( 203)>] abstract member Document : [<return: MarshalAs(UnmanagedType.IDispatch)>] obj
    [<DispId( 204)>] abstract member TopLevelContainer : bool
    [<DispId( 205)>] abstract member Type : string
    [<DispId( 206)>] abstract member Left : int with get, set
    [<DispId( 207)>] abstract member Top : int with get, set
    [<DispId( 208)>] abstract member Width : int with get, set
    [<DispId( 209)>] abstract member Height : int with get, set
    [<DispId( 210)>] abstract member LocationName : string
    [<DispId( 211)>] abstract member LocationURL : string
    [<DispId( 212)>] abstract member Busy : bool
    // IWebBrowserApp members
    [<DispId( 300)>] abstract member Quit : unit -> unit
    [<DispId( 301)>] abstract member ClientToWindow : [<Out>] pcx : int byref * [<Out>] pcy : int byref -> unit
    [<DispId( 302)>] abstract member PutProperty : [<In>] property : string * [<In>] vtValue : obj -> unit
    [<DispId( 303)>] abstract member GetProperty : [<In>] property : string -> obj
    [<DispId(   0)>] abstract member Name : string
    [<DispId(-515)>] abstract member HWND : int
    [<DispId( 400)>] abstract member FullName : string
    [<DispId( 401)>] abstract member Path : string
    [<DispId( 402)>] abstract member Visible : bool with get, set
    [<DispId( 403)>] abstract member StatusBar : bool with get, set
    [<DispId( 404)>] abstract member StatusText : string with get, set
    [<DispId( 405)>] abstract member ToolBar : int with get, set
    [<DispId( 406)>] abstract member MenuBar : bool with get, set
    [<DispId( 407)>] abstract member FullScreen : bool with get, set
    // IWebBrowser2 members
    [<DispId( 500)>] abstract member Navigate2 : [<In>] URL : obj byref * [<In>] flags : obj byref * [<In>] targetFrameName : obj byref * [<In>] postData : obj byref * [<In>] headers : obj byref -> unit
    [<DispId( 501)>] abstract member QueryStatusWB : [<In>] cmdID : (*OLECMDID*)int -> (*OLECMDF*)int
    [<DispId( 502)>] abstract member ExecWB : [<In>] cmdID :(*OLECMDID*)int * [<In>] cmdexecopt : (*OLECMDEXECOPT*)int * pvaIn : obj byref * [<Out>] pvaOut : obj byref -> unit
    [<DispId( 503)>] abstract member ShowBrowserBar : [<In>] pvaClsid : obj byref * [<In>] pvarShow : obj byref * [<In>] pvarSize : obj byref -> unit
    [<DispId(-525)>] abstract member ReadyState : WebBrowserReadyState
    [<DispId( 550)>] abstract member Offline : bool with get, set
    [<DispId( 551)>] abstract member Silent : bool with get, set
    [<DispId( 552)>] abstract member RegisterAsBrowser : bool with get, set
    [<DispId( 553)>] abstract member RegisterAsDropTarget : bool with get, set
    [<DispId( 554)>] abstract member TheaterMode : bool with get, set
    [<DispId( 555)>] abstract member AddressBar : bool with get, set
    [<DispId( 556)>] abstract member Resizable : bool with get, set

[<ComImport; Guid("3050f1ff-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLElement =
    [<DispId((*DISPID_IHTMLELEMENT_OFFSETWIDTH*)0x800103F2)>]
    abstract member offsetWidth : (*long*)int
    [<DispId((*DISPID_IHTMLELEMENT_OFFSETHEIGHT*)0x800103F3)>]
    abstract member offsetHeight : (*long*)int

[<ComImport; Guid("332c4427-26cb-11d0-b483-00c04fd90119"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLWindow2 =
    [<DispId((*DISPID_IHTMLWINDOW2_SCROLLTO*)1168)>]
    abstract member scrollTo : x : int * y : int -> unit

[<ComImport; Guid("3050f2e3-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLStyleSheet =
    [<DispId((*DISPID_IHTMLSTYLESHEET_CSSTEXT*)1014)>]
    abstract member cssText : string with get, set

[<ComImport; Guid("332c4425-26cb-11d0-b483-00c04fd90119"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLDocument2 =
    [<DispId((*DISPID_IHTMLDOCUMENT2_PARENTWINDOW*)1034)>]
    abstract member parentWindow : IHTMLWindow2
    [<DispId((*DISPID_IHTMLDOCUMENT2_CREATESTYLESHEET*)1071)>]
    abstract member createStyleSheet : bstrHref : string * lIndex : int -> IHTMLStyleSheet

[<ComImport; Guid("3050f485-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLDocument3 =
    [<DispId((*DISPID_IHTMLDOCUMENT3_GETELEMENTBYID*)1088)>]
    abstract member getElementById : v : string -> obj

[<ComImport; Guid("3050f6db-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLFrameBase2 =
    [<DispId((*DISPID_IHTMLFRAMEBASE2_CONTENTWINDOW*)0x80010BC1)>]
    abstract member contentWindow : IHTMLWindow2

[<ComImport; Guid("3050f25f-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLEmbedElement =
    interface
    end

[<ComImport; Guid("3050f24f-98b5-11cf-bb82-00aa00bdce0b"); InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private IHTMLObjectElement =
    [<DispId((*DISPID_IHTMLOBJECTELEMENT_OBJECT*)0x80010BB9)>]
    abstract member ``object`` : [<return: MarshalAs(UnmanagedType.IDispatch)>] obj

[<ComImport; Guid "34A715A0-6587-11D0-924A-0020AFC7AC4D"; InterfaceType(ComInterfaceType.InterfaceIsIDispatch)>]
type private DWebBrowserEvents2 =
    [<DispId(273)>] abstract member NewWindow3 : [<In; Out; MarshalAs(UnmanagedType.IDispatch)>] ppDisp : obj byref * [<In; Out; MarshalAs(UnmanagedType.VariantBool)>] Cancel : bool byref * dwFlags : uint32 * bstrUrlContext : string * bstrUrl : string -> unit

[<StructLayout(LayoutKind.Sequential, Pack = 4); AllowNullLiteral>]
type DVTARGETDEVICE =
    val tdSize : int
    val tdDriverNameOffset : uint16
    val tdDeviceNameOffset : uint16
    val tdPortNameOffset : uint16
    val tdExtDevmodeOffset : uint16
    new () = { tdSize = Marshal.SizeOf typeof<DVTARGETDEVICE>; tdDriverNameOffset = 0us; tdDeviceNameOffset = 0us; tdPortNameOffset = 0us; tdExtDevmodeOffset = 0us; }

[<StructLayout(LayoutKind.Sequential, Pack = 4); AllowNullLiteral>]
type RECT =
    val left : int
    val top : int
    val width : int
    val height : int
    new (left, top, width, height) = { left = left; top = top; width = width; height = height }

[<ComImport; Guid("0000010d-0000-0000-C000-000000000046"); InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type IViewObject =
    abstract member Draw : [<In; MarshalAs(UnmanagedType.U4)>] dwDrawAspect : int * lindex : int * pvAspect : nativeint * [<In>] ptd : DVTARGETDEVICE * hdcTargetDev : nativeint * hdcDraw : nativeint * [<In>] lprcBounds : RECT * [<In>] lprcWBounds : RECT * pfnContinue : nativeint * [<In>] dwContinue : nativeint -> unit
    abstract member GetColorSet : [<In; MarshalAs(UnmanagedType.U4)>] dwDrawAspect : int * lindex : int * pvAspect : nativeint * [<In>] ptd : obj * hicTargetDev : nativeint * [<Out>] ppColorSet : obj -> unit
    abstract member Freeze : [<In; MarshalAs(UnmanagedType.U4)>] dwDrawAspect : int * lindex : int * pvAspect : nativeint * [<Out>] pdwFreeze : nativeint -> unit
    abstract member Unfreeze : [<In; MarshalAs(UnmanagedType.U4)>] dwFreeze : int -> unit
    [<PreserveSig>]
    abstract member SetAdvise : [<In; MarshalAs(UnmanagedType.U4)>] aspects : int * [<In; MarshalAs(UnmanagedType.U4)>] advf : int * [<In; MarshalAs(UnmanagedType.Interface)>] pAdvSink : obj -> unit
    [<PreserveSig>]
    abstract member GetAdvise : [<In; Out; MarshalAs(UnmanagedType.LPArray)>] paspects : int[] * [<In; Out; MarshalAs(UnmanagedType.LPArray)>] advf : int[] * [<In; Out; MarshalAs(UnmanagedType.LPArray)>] pAdvSink : obj[] -> unit

[<StructLayout(LayoutKind.Sequential)>]
type private POINT =
    val mutable x : int
    val mutable y : int

[<StructLayout(LayoutKind.Sequential)>]
type private DOCHOSTUIINFO =
    val cbSize : int
    val mutable dwFlags : int
    val mutable dwDoubleClick : int
    val dwReserved1 : int
    val dwReserved2 : int
    new (dwFlags, dwDoubleClick) = { cbSize = Marshal.SizeOf typeof<DOCHOSTUIINFO>; dwFlags = dwFlags; dwDoubleClick = dwDoubleClick; dwReserved1 = 0; dwReserved2 = 0 }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type private MSG =
    val hwnd : nativeint
    val message : int
    val wParam : nativeint
    val lParam : nativeint
    val time : int
    val pt_x : int
    val pt_y : int

[<ComImport; Guid "BD3F23C0-D43E-11CF-893B-00AA00BDCE1A"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private IDocHostUIHandler =
    [<PreserveSig>] abstract member ShowContextMenu : dwID : uint32 * pt : POINT * [<MarshalAs(UnmanagedType.Interface)>] pcmdtReserved : obj * [<MarshalAs(UnmanagedType.Interface)>] pdispReserved : obj -> int
    [<PreserveSig>] abstract member GetHostInfo : [<In; Out>] info : DOCHOSTUIINFO -> int
    [<PreserveSig>] abstract member ShowUI : dwID : int * [<MarshalAs(UnmanagedType.Interface)>] activeObject : obj * [<MarshalAs(UnmanagedType.Interface)>] commandTarget : obj * [<MarshalAs(UnmanagedType.Interface)>] frame : obj * [<MarshalAs(UnmanagedType.Interface)>] doc : obj -> int
    [<PreserveSig>] abstract member HideUI : unit -> int
    [<PreserveSig>] abstract member UpdateUI : unit -> int
    [<PreserveSig>] abstract member EnableModeless : [<MarshalAs(UnmanagedType.Bool)>] fEnable : bool -> int
    [<PreserveSig>] abstract member OnDocWindowActivate : [<MarshalAs(UnmanagedType.Bool)>] fActivate : bool -> int
    [<PreserveSig>] abstract member OnFrameWindowActivate : [<MarshalAs(UnmanagedType.Bool)>] fActivate : bool -> int
    [<PreserveSig>] abstract member ResizeBorder : rect : RECT * [<MarshalAs(UnmanagedType.Interface)>] doc : obj * fFrameWindow : bool -> int
    [<PreserveSig>] abstract member TranslateAccelerator : [<In>] msg : MSG byref * [<MarshalAs(UnmanagedType.LPStruct)>] group : Guid * nCmdID : int -> int
    [<PreserveSig>] abstract member GetOptionKeyPath : [<Out; MarshalAs(UnmanagedType.LPArray)>] pbstrKey : string[] * dw : uint32 -> int
    [<PreserveSig>] abstract member GetDropTarget : [<MarshalAs(UnmanagedType.Interface)>] pDropTarget : obj * [<Out; MarshalAs(UnmanagedType.Interface)>]  ppDropTarget : obj byref -> int
    [<PreserveSig>] abstract member GetExternal : [<Out; MarshalAs(UnmanagedType.Interface)>]  ppDispatch : obj byref -> int
    [<PreserveSig>] abstract member TranslateUrl : dwTranslate : uint32 * [<MarshalAs(UnmanagedType.LPWStr)>] strURLIn : string * [<Out; MarshalAs(UnmanagedType.LPWStr)>] pstrURLOut : string byref -> int
    [<PreserveSig>] abstract member FilterDataObject : pDO : ComTypes.IDataObject * [<Out>] ppDORet : ComTypes.IDataObject byref -> int

[<ComImport; Guid "3050F3F0-98B5-11CF-BB82-00AA00BDCE0B"; InterfaceType(ComInterfaceType.InterfaceIsIUnknown)>]
type private ICustomDoc =
    abstract member SetUIHandler : pUIHandler : IDocHostUIHandler -> unit

type private DocHostUIHandler (webBrowser : WebBrowser) =
    [<Literal>]
    static let S_OK = 0x00000000
    [<Literal>]
    static let S_FALSE = 0x00000001
    [<Literal>]
    static let E_NOTIMPL = 0x80004001
    static let showContextMenu = lazy(typeof<WebBrowser>.GetMethod("ShowContextMenu", BindingFlags.NonPublic ||| BindingFlags.Instance))

    interface IDocHostUIHandler with
        override this.ShowContextMenu (dwID, pt, pcmdtReserved, pdispReserved) =
            if webBrowser.IsWebBrowserContextMenuEnabled then S_FALSE else
            if pt.x = 0 && pt.y = 0 then
                pt.x <- -1
                pt.y <- -1
            // webBrowser.ShowContextMenu(pt.x, pt.y)
            showContextMenu.Force().Invoke(webBrowser, [| pt.x; pt.y |]) |> ignore
            S_OK
        override this.GetHostInfo (info) =
            info.dwDoubleClick <- (*DOCHOSTUIDBLCLK_DEFAULT*)0
            info.dwFlags <- (*DOCHOSTUIFLAG_NO3DOUTERBORDER*)0x00200000 ||| (*DOCHOSTUIFLAG_DISABLE_SCRIPT_INACTIVE*)0x00000010 ||| (*DOCHOSTUIFLAG_DPI_AWARE*)0x40000000
                            ||| (if webBrowser.ScrollBarsEnabled then (*DOCHOSTUIFLAG_FLAT_SCROLLBAR*)0x00000080 else (*DOCHOSTUIFLAG_SCROLL_NO*)0x00000008)
                            ||| (if Application.RenderWithVisualStyles then (*DOCHOSTUIFLAG_THEME*)0x00040000 else (*DOCHOSTUIFLAG_NOTHEME*)0x00080000)
            S_OK
        override this.EnableModeless (fEnable) =
            E_NOTIMPL
        override this.ShowUI (dwID, activeObject, commandTarget, frame, doc) =
            S_FALSE
        override this.HideUI () =
            E_NOTIMPL
        override this.UpdateUI () =
            E_NOTIMPL
        override this.OnDocWindowActivate (fActivate) =
            E_NOTIMPL
        override this.OnFrameWindowActivate (fActivate) =
            E_NOTIMPL
        override this.ResizeBorder (rect, doc, fFrameWindow) =
            E_NOTIMPL
        override this.GetOptionKeyPath (pbstrKey, dw) =
            E_NOTIMPL
        override this.GetDropTarget (pDropTarget, ppDropTarget) =
            ppDropTarget <- null
            E_NOTIMPL
        override this.GetExternal (ppDispatch) =
            ppDispatch <- webBrowser.ObjectForScripting
            S_OK
        override this.TranslateAccelerator (msg, group, nCmdID) =
            if webBrowser.WebBrowserShortcutsEnabled then S_FALSE else
            let keyCode = int msg.wParam ||| int Control.ModifierKeys
            if msg.message <> (*WM_CHAR*)0x0102 && Enum.IsDefined(typeof<Shortcut>, keyCode) then S_OK else S_FALSE
        override this.TranslateUrl (dwTranslate, strUrlIn, pstrUrlOut) =
            pstrUrlOut <- null
            S_FALSE
        override this.FilterDataObject (pDO, ppDORet) =
            ppDORet <- null
            S_FALSE

type NewWindow3EventArgs () =
    inherit EventArgs ()
    member val ppDisp = null with get, set
    member val Cancel = false with get, set

type WebBrowser2 () as self =
    inherit WebBrowser ()
    let newWindow3 = Event<_>()
    let closing = Event<_>()
    let cookie = lazy(AxHost.ConnectionPointCookie(self.ActiveXInstance, EventHelper self, typeof<DWebBrowserEvents2>))
    do
        // use ICustomDoc.SetUIHandler()
        //   http://www.codeproject.com/Articles/2491/Using-MSHTML-Advanced-Hosting-Interfaces
        //   http://top.freespace.jp/ecosoft/tips_cs/src/009_cs.html
        // another approach, .NET4's ICustomQueryInterface.GetInterface()
        //   http://stackoverflow.com/questions/15515581/why-my-implementation-of-idochostuihandler-is-ignored
        self.Navigate "about:blank"
        DocHostUIHandler self |> (self.DomDocument :?> ICustomDoc).SetUIHandler

    member this.DomDocument : obj =
        (this.ActiveXInstance :?> IWebBrowser2).Document
    member this.AddStyleSheet(cssText) =
        (this.DomDocument :?> IHTMLDocument2).createStyleSheet("", -1).cssText <- cssText
    member this.GetApplication() =
        let wb = this.ActiveXInstance :?> IWebBrowser2
        wb.RegisterAsBrowser <- true
        wb.Application
    static member GetCapture(element : obj) =
        let element = element :?> IHTMLElement
        let size = Size(element.offsetWidth, element.offsetHeight)
        let view = match element with
                   | :? IHTMLObjectElement as objectElement -> objectElement.``object`` :?> IViewObject
                   | :? IHTMLEmbedElement -> element :?> IViewObject
                   | _ -> failwith "unknown element"
        let ptd = DVTARGETDEVICE()
        let bounds = RECT(0, 0, size.Width, size.Height)
        let capture hdc =
            view.Draw((*DVASPECT_CONTENT*)1, 0, 0n, ptd, 0n, hdc, bounds, null, 0n, 0n)
        size, capture
    static member GetElementById((document : obj), id) =
        let element = (document :?> IHTMLDocument3).getElementById id
        if element = null then None else Some element
    static member GetFrameDocument(iframe : obj) =
        (((iframe :?> IHTMLFrameBase2).contentWindow :?> IServiceProvider).QueryService(typeof<IWebBrowserApp>.GUID, typeof<IWebBrowser2>.GUID) :?> IWebBrowser2).Document
    member this.Zoom(percent : int) =
        let mutable old = null
        (this.ActiveXInstance :?> IWebBrowser2).ExecWB((*OLECMDID_OPTICAL_ZOOM*)63, (*OLECMDEXECOPT_DODEFAULT*)0, percent :> obj |> ref, &old)
        (this.DomDocument :?> IHTMLDocument2).parentWindow.scrollTo(0, 0)
        old :?> int
        
    [<CLIEvent>]
    member this.NewWindow3 = newWindow3.Publish
    [<CLIEvent>]
    member this.Closing = closing.Publish
    member internal this.OnNewWindow3 e =
        newWindow3.Trigger e
    override this.CreateSink() =
        cookie.Force() |> ignore
        base.CreateSink()
    override this.DetachSink() =
        base.DetachSink()
        if cookie.IsValueCreated then
            cookie.Value.Disconnect()
    override this.WndProc(m) =
        if m.Msg = 0x210(*WM_PARENTNOTIFY*) && int m.WParam = 2(*WM_DESTROY*) then
            closing.Trigger(this, EventArgs.Empty)
        base.WndProc(&m)

and private EventHelper (webBrowser : WebBrowser2) =
    inherit StandardOleMarshalObject ()
    interface DWebBrowserEvents2 with
        member this.NewWindow3 (ppDisp, cancel, dwFlags, bstrUrlContext, bstrUrl) =
            let e = NewWindow3EventArgs()
            webBrowser.OnNewWindow3 e
            ppDisp <- e.ppDisp
            cancel <- e.Cancel
