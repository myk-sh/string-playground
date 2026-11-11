# String laboratory

Open `Assets/Scenes/StringLaboratory.unity` and press Play. The menu **String Lab > Open Playground** opens it too. UI and the 3D bench are constructed at runtime, so the full lab appears in Play mode.

- Space or **Pluck String** plays the current settings. Changes take effect on the next pluck, except output volume and room effects.
- **Store A**, change one variable, then press **A** / **Play A** to compare the original. Playback uses that snapshot's room too.
- **Auto** plucks every three seconds. **Stop** stops playback and auto mode.
- **Retune to A3** adjusts reference tension for 220 Hz at the current environmental settings, within the slider range.
- **Slow motion** runs the exaggerated string animation at 0.8% of physical speed; sound remains at its real frequency.
- **How it works** contains suggested experiments and model assumptions.

## Physical model

For a homogeneous circular string, area A = pi d² / 4 and linear density mu = rho A.
The ideal fundamental is sqrt(T / mu) / (2L). Bending stiffness adds partial stretching:

`f_n = n sqrt(T/mu) / (2L) sqrt(1 + B n²)`

`B = pi² E I / (T L²)`, with second moment `I = pi r⁴ / 4`.

A triangular initial displacement gives modal displacement proportional to sin(n pi p)/n². The synthesized bridge-velocity approximation weights the modes by sin(n pi p)/n with a gentle high frequency rolloff. Each mode decays exponentially, with faster high frequency decay. Up to 48 modes are rendered into an eight-second, 32 kHz mono clip; modes above 45% of sample rate are omitted. A short attack/release avoids buffer-edge clicks. Output is conservatively bounded using the sum of modal amplitudes; strength controls gain. Room sound uses Unity's built-in reverb presets. The bar chart is an initial predicted dry spectrum, not a live FFT; the waveform reads the actual generated dry buffer.

At fixed endpoints the simplified tension offset is `-E A (alpha deltaTemperature + moistureCoefficient deltaRH)`. Metal has no direct humidity coefficient. Tension values are referenced to 20 C / 50% RH. A nonpositive effective tension is shown as slack and does not generate a pitched tone. Values are not restricted by tensile strength, so some combinations would break a real string. A positive floor is used for numerical readouts near slackness.

Material density and modulus are representative values, not a measured dataset. Damping, moisture coefficients and environmental response are illustrative. This is a comparative educational synthesizer, not an instrument-design predictor. It does not model wound strings, instrument bodies, changes in humidity-dependent density/modulus, moving supports, bow friction, or breakage. Temperature changes string tension in this model; room reverb changes sound propagation without changing string pitch.

Basis: [UNSW Music Acoustics: Strings, standing waves and harmonics](https://phys.unsw.edu.au/jw/strings.html), [UNSW: Strings and wires](https://phys.unsw.edu.au/music/people/publications/Fletcher1999a.pdf).

## Code

- `StringPhysics.cs`: pure calculations and modal audio renderer. Render is called on a worker task with a copied settings snapshot.
- `StringPlayground.cs`: controls, playback, 3D visualization and guided experiments.
- `LabPlot.cs`: UI mesh plots.
- `Editor/StringLabSetup.cs`: scene setup and menu command.

No external services, generated sound assets, or Unity AI subscription are required.
