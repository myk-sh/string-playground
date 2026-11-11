using System;
using System.Collections.Generic;
using System.Linq;
using StringLab;

// Run through Unity's script eval --file Verification/PhysicsChecks.cs.
var passed = new List<string>();
void Check(bool condition, string name) { if (!condition) throw new Exception(name); passed.Add(name); }
var s = new StringSettings();
double f = StringPhysics.Frequency(s);
Check(f > 248 && f < 249, "Default steel fundamental is approximately 248.4 Hz");
var thick = s.Copy(); thick.diameter *= 2;
Check(Math.Abs(StringPhysics.Frequency(thick) / f - .5) < .005, "Twice the diameter approximately halves pitch");
var longer = s.Copy(); longer.length *= 2;
Check(Math.Abs(StringPhysics.Frequency(longer) / f - .5) < .001, "Twice the length approximately halves pitch");
var taut = s.Copy(); taut.tension *= 2;
Check(Math.Abs(StringPhysics.Frequency(taut) / f - Math.Sqrt(2)) < .001, "Twice the tension raises pitch by approximately sqrt(2)");
var warm = s.Copy(); warm.temperature = 30;
Check(StringPhysics.Frequency(warm) < f, "Warming a string at fixed endpoints lowers tension and pitch");
var humid = s.Copy(); humid.humidity = 80;
Check(StringPhysics.Frequency(humid) == f, "Metal has no direct humidity effect");
humid.material = 1; var nylon = humid.Copy(); nylon.humidity = 50;
Check(StringPhysics.Frequency(humid) < StringPhysics.Frequency(nylon), "Nylon's illustrative moisture response lowers pitch");
var center = s.Copy(); center.position = .5f;
Check(Math.Abs(StringPhysics.Weight(center,2)) < 1e-10 && Math.Abs(StringPhysics.Weight(center,4)) < 1e-10, "Center pluck suppresses even modes");
Check(StringPhysics.Frequency(s,10) > 10*f, "Stiffness stretches upper partials");
var peak = 0f;
for (int material=0; material<4; material++)
{
    foreach(bool extreme in new[]{false,true})
    {
        var config = new StringSettings { material=material };
        if(extreme) {config.diameter=1.5f;config.length=.25f;config.tension=160;config.temperature=0;config.humidity=10;config.position=.08f;config.strength=1;config.damping=.2f;}
        var audio = StringPhysics.Render(config);
        Check(audio.Length == 256000 && audio.All(v=>!float.IsNaN(v) && !float.IsInfinity(v) && Math.Abs(v)<=.651), "Finite bounded audio: material "+material+", extreme "+extreme);
        peak = Math.Max(peak,audio.Max(v=>Math.Abs(v)));
    }
}
var slack = new StringSettings { diameter=1.5f,tension=5,temperature=40 };
Check(StringPhysics.RawTension(slack)<=0 && StringPhysics.Render(slack).All(v=>v==0), "Slack strings produce silence");
var quiet=s.Copy();quiet.strength=.3f;var loud=s.Copy();loud.strength=.6f;
var qa=StringPhysics.Render(quiet);var la=StringPhysics.Render(loud);
Check(qa.Zip(la,(a,b)=>Math.Abs(b-2*a)).Max()<.00001, "Pluck strength scales the generated sound");
return new { passed=passed.Count, checks=passed.ToArray(), peak };
