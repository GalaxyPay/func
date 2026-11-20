// The chime played when this node creates a block: a soft low thump, then a quick
// rising major arpeggio of bell tones with a short echo, synthesized so there is
// no audio file to ship.
//
// Browsers' autoplay rules keep a page silent until it has had a click or key
// press. So the audio is unlocked on the first one anywhere on the page, and from
// then on the chime plays even with the tab in the background or the window
// unfocused. A chime that comes before that is skipped, not saved up.

let audioCtx: AudioContext | undefined;

function context() {
  audioCtx ??= new (
    window.AudioContext || (window as any).webkitAudioContext
  )();
  return audioCtx;
}

const UNLOCK_EVENTS = ["pointerdown", "keydown"] as const;

function unlock() {
  try {
    const ctx = context();
    const done = () => {
      if (ctx.state !== "running") return;
      for (const e of UNLOCK_EVENTS) window.removeEventListener(e, unlock);
    };
    if (ctx.state === "running") done();
    else ctx.resume().then(done, () => {});
  } catch (err) {
    console.error(err);
  }
}

for (const e of UNLOCK_EVENTS) window.addEventListener(e, unlock);

// C6 E6 G6 C7: bright and unmistakably "good news" without being shrill.
const NOTES = [1046.5, 1318.5, 1568.0, 2093.0];
const STEP = 0.075;

export function playBlockSound() {
  try {
    const ctx = context();
    // Still locked: a suspended context's clock is stopped, so anything scheduled
    // now would all play at once on the next click.
    if (ctx.state !== "running") return;
    const now = ctx.currentTime + 0.01;

    // Keeps overlapping notes and the echo from clipping.
    const limiter = ctx.createDynamicsCompressor();
    limiter.threshold.value = -12;
    limiter.ratio.value = 8;
    const master = ctx.createGain();
    master.gain.value = 0.55;
    master.connect(limiter).connect(ctx.destination);

    // A short, darkened echo for a sense of space.
    const echo = ctx.createDelay();
    echo.delayTime.value = 0.14;
    const feedback = ctx.createGain();
    feedback.gain.value = 0.28;
    const tone = ctx.createBiquadFilter();
    tone.type = "lowpass";
    tone.frequency.value = 3200;
    const wet = ctx.createGain();
    wet.gain.value = 0.35;
    echo.connect(tone).connect(feedback).connect(echo);
    tone.connect(wet).connect(master);

    const dry = ctx.createGain();
    dry.connect(master);
    dry.connect(echo);

    thump(ctx, dry, now);
    NOTES.forEach((freq, i) => {
      const last = i === NOTES.length - 1;
      bell(
        ctx,
        dry,
        now + i * STEP,
        freq,
        last ? 0.5 : 0.32,
        last ? 1.1 : 0.35
      );
    });
    sparkle(ctx, dry, now + (NOTES.length - 1) * STEP);
  } catch (err) {
    console.error(err);
  }
}

// One bell strike: a sine fundamental plus two quieter, slightly inharmonic
// partials, which is what makes it ring like metal instead of beep.
function bell(
  ctx: AudioContext,
  out: AudioNode,
  at: number,
  freq: number,
  level: number,
  decay: number
) {
  const partials = [
    { ratio: 1, level: 1, decay: 1 },
    { ratio: 2.76, level: 0.18, decay: 0.45 },
    { ratio: 5.4, level: 0.06, decay: 0.25 },
  ];
  for (const p of partials) {
    const osc = ctx.createOscillator();
    const g = ctx.createGain();
    osc.type = "sine";
    osc.frequency.value = freq * p.ratio;
    const end = at + decay * p.decay;
    g.gain.setValueAtTime(0.0001, at);
    g.gain.exponentialRampToValueAtTime(level * p.level, at + 0.004);
    g.gain.exponentialRampToValueAtTime(0.0001, end);
    osc.connect(g).connect(out);
    osc.start(at);
    osc.stop(end + 0.02);
  }
}

// A soft low drop under the first note, so it lands with some weight.
function thump(ctx: AudioContext, out: AudioNode, at: number) {
  const osc = ctx.createOscillator();
  const g = ctx.createGain();
  osc.type = "sine";
  osc.frequency.setValueAtTime(180, at);
  osc.frequency.exponentialRampToValueAtTime(55, at + 0.14);
  g.gain.setValueAtTime(0.0001, at);
  g.gain.exponentialRampToValueAtTime(0.6, at + 0.006);
  g.gain.exponentialRampToValueAtTime(0.0001, at + 0.18);
  osc.connect(g).connect(out);
  osc.start(at);
  osc.stop(at + 0.2);
}

// A faint airy shimmer over the top note.
function sparkle(ctx: AudioContext, out: AudioNode, at: number) {
  const length = 0.35;
  const buf = ctx.createBuffer(
    1,
    Math.ceil(ctx.sampleRate * length),
    ctx.sampleRate
  );
  const data = buf.getChannelData(0);
  for (let i = 0; i < data.length; i++) data[i] = Math.random() * 2 - 1;
  const noise = ctx.createBufferSource();
  noise.buffer = buf;
  const hp = ctx.createBiquadFilter();
  hp.type = "highpass";
  hp.frequency.value = 7000;
  const g = ctx.createGain();
  g.gain.setValueAtTime(0.0001, at);
  g.gain.exponentialRampToValueAtTime(0.08, at + 0.02);
  g.gain.exponentialRampToValueAtTime(0.0001, at + length);
  noise.connect(hp).connect(g).connect(out);
  noise.start(at);
  noise.stop(at + length);
}
