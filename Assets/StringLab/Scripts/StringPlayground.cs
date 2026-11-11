using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;

namespace StringLab
{
    public class StringPlayground : MonoBehaviour
    {
        public TMP_FontAsset font;
        public StringSettings settings = new StringSettings();
        public double CurrentFrequency => StringPhysics.Frequency(settings);
        public bool IsSynthesizing { get; private set; }
        public AudioSource Sound => source;
        readonly Color ink = new Color(.89f,.92f,.87f), muted = new Color(.52f,.64f,.64f);
        readonly Color panel = new Color(.057f,.09f,.102f), teal = new Color(.35f,.85f,.75f), orange = new Color(1,.64f,.36f);
        readonly List<Action> refreshControls = new List<Action>();
        readonly List<Material> ownedMaterials = new List<Material>();
        RectTransform canvas;
        TMP_Text pitchText, noteText, statsText, insightText, statusText, compareText, materialText, environmentText;
        LabPlot spectrum, wave;
        AudioSource source;
        AudioReverbFilter reverb;
        AudioClip clip;
        StringSettings reference, sounding;
        LineRenderer line, ghost;
        Transform marker, rig;
        Camera labCamera;
        float started = -100, nextPluck, volume = .65f;
        bool autoPluck, slowMotion = true, helpVisible;
        int playbackVersion;
        GameObject help;
        string lastAction = "Ready. Pluck the string to begin.";

        void Awake()
        {
            Application.runInBackground = true;
            source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 0; source.volume = volume;
            reverb = gameObject.AddComponent<AudioReverbFilter>();
            BuildWorld(); BuildUI(); Refresh();
        }
        void Update()
        {
            var kb = Keyboard.current;
            if (kb != null)
            {
                if (kb.spaceKey.wasPressedThisFrame) Pluck();
                if (kb.aKey.wasPressedThisFrame) PlayReference();
                if (kb.escapeKey.wasPressedThisFrame && helpVisible) ToggleHelp();
            }
            if (autoPluck && Time.unscaledTime > nextPluck && !IsSynthesizing) { Pluck(); nextPluck = Time.unscaledTime + 3; }
            AnimateString();
            if (wave != null && source.isPlaying) { wave.playhead = source.time; wave.SetVerticesDirty(); }
        }
        void BuildWorld()
        {
            labCamera = Camera.main;
            if (!labCamera) { var c = new GameObject("Lab Camera"); labCamera = c.AddComponent<Camera>(); c.tag = "MainCamera"; c.AddComponent<AudioListener>(); }
            labCamera.clearFlags = CameraClearFlags.SolidColor; labCamera.backgroundColor = new Color(.035f,.065f,.077f);
            labCamera.transform.position = new Vector3(0,7,-9); labCamera.transform.LookAt(new Vector3(0,0,0));
            labCamera.orthographic = true; labCamera.orthographicSize = 6.35f;
            // The viewport tracks the unobscured central panel in the scaled UI.
            rig = new GameObject("Monochord • physical model").transform;
            var wood = Mat(new Color(.25f,.15f,.095f), .1f);
            var metal = Mat(new Color(.16f,.23f,.25f), .8f);
            var brass = Mat(new Color(.73f,.46f,.20f), .7f);
            Cube("Resonant bench",new Vector3(0,-.28f,0),new Vector3(7.7f,.18f,1.45f),wood);
            Cube("Inset soundboard",new Vector3(0,-.18f,0),new Vector3(6.9f,.09f,1.14f),Mat(new Color(.38f,.25f,.15f),.05f));
            for (int i = 0; i < 17; i++) Cube("Wood grain",new Vector3(0,-.128f,-.53f+i*.065f),new Vector3(6.8f,.005f,.007f),wood);
            foreach (float x in new[]{-3.2f,3.2f})
            {
                Cube("Fixed bridge",new Vector3(x,.09f,0),new Vector3(.12f,.44f,1.1f),brass);
                Cube("End support",new Vector3(x,-.5f,0),new Vector3(.65f,.45f,1.8f),metal);
                var peg = GameObject.CreatePrimitive(PrimitiveType.Cylinder); peg.name="Tuning post"; peg.transform.SetParent(rig);
                peg.transform.position=new Vector3(x*1.12f,.12f,0); peg.transform.localScale=new Vector3(.16f,.33f,.16f); peg.GetComponent<Renderer>().sharedMaterial=metal;
            }
            // Decorative calibration marks beneath the vibrating length.
            for (int i=0;i<=20;i++) Cube("Scale tick",new Vector3(-3.2f+i*.32f,-.12f,-.66f),new Vector3(.015f,.015f,i%5==0?.15f:.06f),brass);
            line = MakeLine("Vibrating string",orange,.025f); ghost = MakeLine("Rest position",new Color(.18f,.36f,.37f),.008f);
            ghost.positionCount=2; ghost.SetPosition(0,new Vector3(-3.2f,.34f,0)); ghost.SetPosition(1,new Vector3(3.2f,.34f,0));
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere).transform; marker.name="Pluck point"; marker.SetParent(rig);
            marker.localScale=Vector3.one*.12f; marker.GetComponent<Renderer>().sharedMaterial=Mat(teal,.35f);
            var light = new GameObject("Warm bench light").AddComponent<Light>(); light.type=LightType.Point; light.transform.position=new Vector3(-2,4,-2); light.color=new Color(1,.77f,.52f); light.intensity=18; light.range=14;
            RenderSettings.ambientLight=new Color(.35f,.46f,.5f);
        }
        Material Mat(Color c,float metallic)
        {
            var m=new Material(Shader.Find("Universal Render Pipeline/Lit")); m.color=c; m.SetFloat("_Metallic",metallic); m.SetFloat("_Smoothness",.4f); ownedMaterials.Add(m); return m;
        }
        void Cube(string name,Vector3 p,Vector3 scale,Material m)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube); go.name=name; go.transform.SetParent(rig); go.transform.position=p; go.transform.localScale=scale; go.GetComponent<Renderer>().sharedMaterial=m;
        }
        LineRenderer MakeLine(string name,Color c,float width)
        {
            var go=new GameObject(name); go.transform.SetParent(rig); var lr=go.AddComponent<LineRenderer>();
            var m=new Material(Shader.Find("Universal Render Pipeline/Unlit")); m.color=c; ownedMaterials.Add(m); lr.sharedMaterial=m;
            lr.startWidth=lr.endWidth=width; lr.positionCount=160; lr.numCapVertices=4; return lr;
        }
        void AnimateString()
        {
            // A full-screen camera avoids URP overlay artifacts from partial viewports.
            float scale=canvas ? canvas.lossyScale.x : 1;
            float designH=Screen.height/scale;
            labCamera.rect=new Rect(0,0,1,1);
            labCamera.orthographicSize=1.65f*designH/260f;
            var s=sounding ?? settings;
            float age=Time.unscaledTime-started;
            double time=slowMotion ? age*.008 : age;
            
            for(int i=0;i<160;i++)
            {
                float x=i/159f; double y=0;
                if(age<StringPhysics.Duration && source.isPlaying)
                    for(int n=1;n<=12;n++) y+=Math.Sin(Math.PI*n*s.position)/(n*n)*Math.Sin(Math.PI*n*x)*Math.Cos(2*Math.PI*StringPhysics.Frequency(s,n)*time)*Math.Exp(-StringPhysics.Decay(s,n)*age);
                line.SetPosition(i,new Vector3(-3.2f+6.4f*x,.34f+(float)y*.5f*s.strength,0));
            }
            line.startWidth=line.endWidth=Mathf.Lerp(.012f,.055f,Mathf.InverseLerp(.15f,1.5f,settings.diameter));
            marker.position=new Vector3(-3.2f+6.4f*settings.position,.34f,-.1f);
        }
        void BuildUI()
        {
            var go=new GameObject("String Lab UI",typeof(RectTransform),typeof(Canvas),typeof(UnityEngine.UI.CanvasScaler),typeof(UnityEngine.UI.GraphicRaycaster));
            canvas=go.GetComponent<RectTransform>(); go.GetComponent<Canvas>().renderMode=RenderMode.ScreenSpaceOverlay;
            var scaler=go.GetComponent<UnityEngine.UI.CanvasScaler>(); scaler.uiScaleMode=UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution=new Vector2(1600,1000); scaler.screenMatchMode=UnityEngine.UI.CanvasScaler.ScreenMatchMode.Expand;
            if (!FindFirstObjectByType<EventSystem>()) new GameObject("EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));
            // Centered design root makes all panels remain visible across aspect ratios.
            var root=Rect("Design",canvas,0,0,1600,1000); root.anchorMin=root.anchorMax=new Vector2(.5f,.5f); root.pivot=new Vector2(.5f,.5f); root.anchoredPosition=Vector2.zero;
            Label(root,"ACOUSTICS / INTERACTIVE STUDIES",30,24,800,25,14,teal);
            Label(root,"String laboratory",28,55,850,65,42,ink);
            Label(root,"Change one thing. Hear what happens.",30,121,900,28,18,muted);
            Button(root,"HOW IT WORKS",1300,65,270,46,ToggleHelp,false);
            // Two side control panels.
            var left=Box(root,"String controls",28,174,310,714,panel);
            Label(left,"01  /  THE STRING",20,18,270,28,16,teal);
            materialText=Label(left,"",20,54,270,26,20,ink);
            for(int i=0;i<4;i++) { int index=i; Button(left,StringPhysics.Materials[i].name,20+(i%2)*139,91+(i/2)*42,131,35,()=>{settings.material=index;Refresh();},false,"Material"+i); }
            Slider(left,"Diameter",195,.15f,1.5f,()=>settings.diameter,v=>settings.diameter=v,v=>v.ToString("0.00")+" mm");
            Slider(left,"Speaking length",272,.25f,1.2f,()=>settings.length,v=>settings.length=v,v=>(v*100).ToString("0.0")+" cm");
            Slider(left,"Tension at 20 C / 50% RH",349,5,160,()=>settings.tension,v=>settings.tension=v,v=>v.ToString("0.0")+" N");
            Slider(left,"Pluck position",426,.08f,.92f,()=>settings.position,v=>settings.position=v,v=>(v*100).ToString("0")+" %");
            Slider(left,"Pluck strength",503,.1f,1,()=>settings.strength,v=>settings.strength=v,v=>(v*100).ToString("0")+" %");
            Button(left,"RETUNE TO A3  /  220 Hz",20,591,270,42,Retune,false);
            Label(left,"Fixed endpoints · homogeneous string\nPosition is measured from the left bridge.",20,650,270,48,13,muted);
            var right=Box(root,"Environment",1262,174,310,714,panel);
            Label(right,"02  /  THE SURROUNDINGS",20,18,270,28,16,teal);
            Slider(right,"Temperature",70,0,40,()=>settings.temperature,v=>settings.temperature=v,v=>v.ToString("0")+" C");
            Slider(right,"Relative humidity",147,10,90,()=>settings.humidity,v=>settings.humidity=v,v=>v.ToString("0")+" %");
            Slider(right,"Extra damping",224,.2f,3,()=>settings.damping,v=>settings.damping=v,v=>v.ToString("0.0")+" x");
            Label(right,"ROOM ACOUSTICS",20,313,270,25,13,muted);
            string[] rooms={"Dry","Studio","Hall"};
            for(int i=0;i<3;i++) {int room=i;Button(right,rooms[i],20+i*92,351,86,36,()=>{settings.room=room;Refresh();},false,"Room"+i);}
            environmentText=Label(right,"",20,404,270,100,16,ink);
            Label(right,"LISTENING LEVEL",20,528,270,25,13,muted);
            Slider(right,"Output volume",560,0,1,()=>volume,v=>{volume=v;source.volume=v;},v=>(v*100).ToString("0")+" %");
            Label(right,"Temperature / humidity response is an\nillustration, not calibrated material data.",20,650,270,48,13,muted);
            // Central experiment surface.
            Label(root,"MONOCHORD  /  ONE STRING, MANY POSSIBILITIES",364,181,870,25,14,muted);
            pitchText=Label(root,"",363,220,600,85,68,ink);
            noteText=Label(root,"",367,311,760,32,19,teal);
            Label(root,"FIXED BRIDGE",376,588,200,25,12,muted);
            Label(root,"FIXED BRIDGE",1090,588,150,25,12,muted);
            Button(root,"PLUCK STRING  [SPACE]",499,551,345,53,Pluck,true);
            Button(root,"STOP",855,551,100,53,Stop,false);
            Button(root,"AUTO / OFF",371,633,202,36,()=>{autoPluck=!autoPluck;nextPluck=0;Refresh();},false,"AutoButton");
            Button(root,"SLOW MOTION / ON",584,633,239,36,()=>{slowMotion=!slowMotion;Refresh();},false,"SlowButton");
            statsText=Label(root,"",845,630,395,48,14,muted);
            var plots=Box(root,"Analysis",363,693,875,195,panel);
            Label(plots,"PREDICTED PARTIALS",16,13,408,25,13,muted);
            Label(plots,"OUTPUT WAVEFORM  /  25 ms",456,13,405,25,13,muted);
            spectrum=Plot(plots,16,49,403,106,true);wave=Plot(plots,456,49,402,106,false);
            Label(plots,"1      3      5      7      9      11     13     15",16,163,410,24,12,muted);
            Label(plots,"Generated dry signal · room added on playback",456,163,405,24,12,muted);
            var footer=Box(root,"Experiment footer",28,909,1544,69,panel);
            statusText=Label(footer,"",18,12,870,46,15,ink);
            Button(footer,"STORE A",962,13,165,42,()=>{reference=settings.Copy();Refresh();lastAction="Reference A saved. Change a control, then compare.";Refresh();},false);
            Button(footer,"PLAY A  [A]",1140,13,165,42,PlayReference,false);
            Button(footer,"RESET",1318,13,205,42,ResetLab,false);
            compareText=Label(root,"",371,354,855,25,14,muted);
            insightText=Label(root,"",371,397,855,75,18,ink);
            BuildHelp(root);
        }
        LabPlot Plot(Transform p,float x,float y,float w,float h,bool bars)
        {
            var r=Rect(bars?"Harmonics":"Waveform",p,x,y,w,h);r.gameObject.AddComponent<CanvasRenderer>();var plot=r.gameObject.AddComponent<LabPlot>();plot.spectrum=bars;plot.color=teal;plot.raycastTarget=false;return plot;
        }
        RectTransform Rect(string name,Transform p,float x,float y,float w,float h)
        {
            var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>();r.SetParent(p,false);
            r.anchorMin=r.anchorMax=new Vector2(0,1);r.pivot=new Vector2(0,1);r.anchoredPosition=new Vector2(x,-y);r.sizeDelta=new Vector2(w,h);return r;
        }
        RectTransform Box(Transform p,string name,float x,float y,float w,float h,Color c)
        {
            var r=Rect(name,p,x,y,w,h);var img=r.gameObject.AddComponent<UnityEngine.UI.Image>();img.color=c;img.raycastTarget=false;return r;
        }
        TMP_Text Label(Transform p,string text,float x,float y,float w,float h,float size,Color c)
        {
            var r=Rect("Label",p,x,y,w,h);var t=r.gameObject.AddComponent<TextMeshProUGUI>();t.font=font;t.text=text;t.fontSize=size;t.color=c;t.raycastTarget=false;
            t.textWrappingMode=TextWrappingModes.Normal;t.overflowMode=TextOverflowModes.Ellipsis;return t;
        }
        void Button(Transform p,string text,float x,float y,float w,float h,Action action,bool primary,string name=null)
        {
            var r=Box(p,name??text,x,y,w,h,primary?orange:new Color(.10f,.17f,.19f));r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            var b=r.gameObject.AddComponent<UnityEngine.UI.Button>();b.targetGraphic=r.GetComponent<UnityEngine.UI.Image>();
            var colors=b.colors;colors.highlightedColor=new Color(.8f,.95f,.91f);colors.pressedColor=new Color(.55f,.75f,.72f);b.colors=colors;
            b.onClick.AddListener(()=>action());var t=Label(r,text,4,0,w-8,h,primary?18:14,primary?panel:ink);t.alignment=TextAlignmentOptions.Center;
        }
        void Slider(Transform p,string title,float y,float min,float max,Func<float> get,Action<float> set,Func<float,string> format)
        {
            Label(p,title,20,y,270,25,14,muted);var value=Label(p,"",20,y+25,270,25,18,ink);value.alignment=TextAlignmentOptions.Right;
            var r=Rect(title,p,20,y+40,270,27);var slider=r.gameObject.AddComponent<UnityEngine.UI.Slider>();slider.minValue=min;slider.maxValue=max;
            var track=Box(r,"Track",0,10,270,5,new Color(.16f,.25f,.27f));
            var fillArea=Rect("Fill area",r,0,10,270,5);var fill=Box(fillArea,"Fill",0,0,270,5,teal);fill.anchorMin=Vector2.zero;fill.anchorMax=Vector2.one;fill.sizeDelta=Vector2.zero;slider.fillRect=fill;
            var area=Rect("Handle area",r,0,0,270,27);var handle=Box(area,"Handle",0,5,12,17,ink);slider.handleRect=handle;slider.targetGraphic=handle.GetComponent<UnityEngine.UI.Image>();handle.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            // A transparent hit surface makes the entire rail draggable.
            var hit=r.gameObject.AddComponent<UnityEngine.UI.Image>();hit.color=Color.clear;hit.raycastTarget=true;
            slider.onValueChanged.AddListener(v=>{set(v);Refresh();});
            refreshControls.Add(()=>{slider.SetValueWithoutNotify(get());value.text=format(get());});
        }
        void Refresh()
        {
            foreach(var refresh in refreshControls)refresh();
            var hz=CurrentFrequency;pitchText.text=hz.ToString("0.0")+" <size=28>Hz</size>";noteText.text=StringPhysics.RawTension(settings)<=0?"SLACK STRING / increase tension or reduce temperature":StringPhysics.Note(hz)+"  /  fundamental";
            materialText.text=StringPhysics.Materials[settings.material].name+" / solid monofilament";
            statsText.text="Effective tension  "+StringPhysics.Tension(settings).ToString("0.0")+" N\nMass per length  "+(StringPhysics.LinearDensity(settings)*1000).ToString("0.000")+" g/m";
            string[] rooms={"Dry · no reverb","Studio · short reflections","Hall · long reverberation"};
            environmentText.text=rooms[settings.room]+"\n\n"+(settings.material==0||settings.material==3?"Metal: humidity has no direct pitch effect in this model.":"Moisture expands this string, reducing its tension at fixed endpoints.");
            insightText.text="At fixed tension, a thicker or denser string sounds lower.\nPluck near a bridge for brighter upper partials.";
            compareText.text=reference==null?"A/B COMPARISON  /  Store a reference below, then change one variable.":"REFERENCE A  /  "+StringPhysics.Materials[reference.material].name+" · "+StringPhysics.Frequency(reference).ToString("0.0")+" Hz     |     Change "+(1200*Math.Log(hz/StringPhysics.Frequency(reference),2)).ToString("+0;-0;0")+" cents";
            statusText.text=lastAction+"\n"+(IsSynthesizing?"Synthesizing the string…":"Changes are heard on the next pluck. Space: pluck · A: reference");
            spectrum.settings=settings.Copy();spectrum.SetVerticesDirty();
            foreach(var r in canvas.GetComponentsInChildren<RectTransform>())
            {
                bool materialButton=r.name.StartsWith("Material"), roomButton=r.name.StartsWith("Room");
                if((materialButton || roomButton) && r.TryGetComponent<UnityEngine.UI.Button>(out var b))
                {
                    bool selected=r.name==(materialButton?"Material"+settings.material:"Room"+settings.room);
                    b.targetGraphic.color=selected?new Color(.17f,.35f,.33f):new Color(.10f,.17f,.19f);
                }
            }
            SetButton("AutoButton",autoPluck?"AUTO / ON":"AUTO / OFF");SetButton("SlowButton",slowMotion?"SLOW MOTION / ON":"SLOW MOTION / OFF");
            reverb.reverbPreset=settings.room==0?AudioReverbPreset.Off:settings.room==1?AudioReverbPreset.Room:AudioReverbPreset.Concerthall;
        }
        void SetButton(string name,string text)
        {
            foreach(var t in canvas.GetComponentsInChildren<RectTransform>())if(t.name==name)t.GetComponentInChildren<TMP_Text>().text=text;
        }
        public void Pluck() { if(!IsSynthesizing)StartCoroutine(Play(settings.Copy(),false)); }
        public void PlayReference()
        {
            if(reference==null){lastAction="Store reference A first, then change a control.";Refresh();return;}
            if(!IsSynthesizing)StartCoroutine(Play(reference.Copy(),true));
        }
        IEnumerator Play(StringSettings s,bool isReference)
        {
            if(StringPhysics.RawTension(s)<=0){lastAction="This string is slack. Raise tension to hear a pitched note.";Refresh();yield break;}
            int version=++playbackVersion;
            IsSynthesizing=true;Refresh();
            Task<float[]> task=Task.Run(()=>StringPhysics.Render(s));
            while(!task.IsCompleted)yield return null;
            IsSynthesizing=false;
            if(version!=playbackVersion){Refresh();yield break;}
            if(task.IsFaulted){lastAction="Sound generation failed. See the Unity Console.";Debug.LogException(task.Exception);Refresh();yield break;}
            source.Stop();if(clip)Destroy(clip);
            clip=AudioClip.Create("String Lab / "+StringPhysics.Materials[s.material].name,task.Result.Length,1,StringPhysics.SampleRate,false);clip.SetData(task.Result,0);
            source.clip=clip;source.volume=volume;
            Refresh();reverb.reverbPreset=s.room==0?AudioReverbPreset.Off:s.room==1?AudioReverbPreset.Room:AudioReverbPreset.Concerthall;
            source.Play();sounding=s;started=Time.unscaledTime;wave.samples=task.Result;wave.playhead=0;wave.SetVerticesDirty();
            lastAction=(isReference?"Playing reference A":"Plucked current string")+" · "+StringPhysics.Frequency(s).ToString("0.0")+" Hz";statusText.text=lastAction+"\nVisualization is exaggerated"+(slowMotion?" and slowed to 0.8% speed.":"; audio runs at true speed.");
        }
        public void Stop(){playbackVersion++;autoPluck=false;source.Stop();started=-100;lastAction="Sound stopped.";Refresh();}
        void Retune()
        {
            double b=StringPhysics.Stiffness(settings)*StringPhysics.Tension(settings);
            double target=Math.Pow(2*settings.length*220,2)*StringPhysics.LinearDensity(settings)-b-StringPhysics.EnvironmentOffset(settings);
            settings.tension=Mathf.Clamp((float)target,5,160);
            lastAction=target<5||target>160?"A3 is outside the tension range for this string. Adjust diameter / length.":"Retuned to A3. Pluck to listen.";Refresh();
        }
        void ResetLab(){Stop();settings=new StringSettings();reference=null;lastAction="Reset to steel at 20 C / 50% RH.";Refresh();}
        void ToggleHelp(){helpVisible=!helpVisible;help.SetActive(helpVisible);}
        void BuildHelp(Transform root)
        {
            var r=Box(root,"About the model",220,150,1160,725,new Color(.065f,.11f,.13f));help=r.gameObject;r.GetComponent<UnityEngine.UI.Image>().raycastTarget=true;
            Label(r,"A small lab for a big sound",38,28,1080,65,36,ink);
            Label(r,"TRY THESE EXPERIMENTS",38,111,1080,30,16,teal);
            Label(r,"01   Store A, then double the diameter. At fixed tension, pitch roughly halves.\n02   Swap steel for nylon. Density changes pitch; damping changes sustain.\n03   Pluck at 50%. Even partials are suppressed. Move near a bridge for brightness.\n04   Warm the string. Fixed endpoints turn thermal expansion into lower tension.\n05   Compare Dry with Hall. The room changes the decay you hear, not string pitch.",38,160,1080,195,20,ink);
            Label(r,"THE MODEL",38,384,1080,30,16,teal);
            Label(r,"Ideal pitch: f = (1 / 2L) × sqrt(T / linear density). Circular area sets mass per length.\n48 decaying modes add bending stiffness and pluck-position coloration; aliased modes are omitted.\nMaterial densities and elastic moduli are representative. Losses, humidity and thermal response\nare illustrative. Endpoints are fixed; there is no body resonance, winding, bowing or string breakage.\nThe partial plot predicts the initial dry spectrum; the waveform shows generated dry audio.\nVibration is enlarged and optionally slowed. Use headphones or speakers at a comfortable level.",38,428,1080,173,18,muted);
            Label(r,"Basis: UNSW Music Acoustics — Strings, standing waves and harmonics",38,626,890,25,14,muted);
            Button(r,"BACK TO LAB",880,650,240,44,ToggleHelp,true);help.SetActive(false);
        }
        void OnDestroy(){if(clip)Destroy(clip);foreach(var m in ownedMaterials)if(m)Destroy(m);}
    }
}
