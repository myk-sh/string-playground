using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using StringLab;

var lab=UnityEngine.Object.FindFirstObjectByType<StringPlayground>();
if(lab==null)throw new Exception("Enter the laboratory's Play mode first.");
var saved=lab.settings.Copy();
var results=new System.Collections.Generic.List<string>();
UnityEngine.UI.Button Button(string name)=>UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Button>().Single(b=>b.name==name);
void Check(bool condition,string label){if(!condition)throw new Exception(label);results.Add(label);}
// Restore user's parameters after exercising control callbacks.
try
{
    lab.settings=new StringSettings();
    Button("RETUNE TO A3  /  220 Hz").onClick.Invoke();
    Check(Math.Abs(lab.CurrentFrequency-220)<.01,"Retune control reaches A3");
    var diameter=UnityEngine.Object.FindObjectsByType<UnityEngine.UI.Slider>().Single(s=>s.name=="Diameter");
    double before=lab.CurrentFrequency;
    diameter.value=.66f;
    Check(lab.CurrentFrequency<before*.6,"Diameter slider changes the physical pitch");
    var harmonics=UnityEngine.Object.FindObjectsByType<LabPlot>().Single(p=>p.spectrum);
    Canvas.ForceUpdateCanvases();
    var mesh=harmonics.canvasRenderer.GetMesh();
    Check(mesh.vertexCount>64,"Harmonic plot has rendered bars and grid");

    lab.Pluck();
    await Wait.Until(()=>!lab.IsSynthesizing,timeoutMs:10000);
    Check(lab.Sound.clip!=null && lab.Sound.isPlaying,"Pluck creates a clip and starts playback");
    Check(lab.Sound.clip.samples==256000,"Playback contains the complete generated buffer");
    Button("HOW IT WORKS").onClick.Invoke();
    var help=GameObject.Find("About the model");
    Check(help!=null && help.activeInHierarchy,"Explanation opens");
    Canvas.ForceUpdateCanvases();
    Check(help.GetComponentsInChildren<TMPro.TMP_Text>().All(t=>!t.isTextOverflowing),"Explanation text fits its bounds");
    Button("BACK TO LAB").onClick.Invoke();
    Check(!help.activeSelf,"Explanation closes");
}
finally
{
    lab.Stop();lab.settings=saved;
    // Refresh without modifying the user's reference A.
    typeof(StringPlayground).GetMethod("Refresh",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(lab,null);
}
return new {passed=results.Count,checks=results.ToArray()};
