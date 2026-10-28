// A client sky file (Resources/SKY/*.xml) as keyframes by time of day, for lighting the viewport like the game.
//
// <SunLight> has <Color Time R G B Brightness> and <Direction Time Heading Pitch>, <Fog> <Color Time R G B> and
// <Range Time Near Far>, <Ambient> <Texture Time File Brightness> (a gradient: sky light on top, ground below).

function hours(time) {
  const [h, m] = time.split(":").map(Number);
  return h + (m || 0) / 60;
}

function track(xml, section, tag) {
  const block = xml.match(new RegExp(`<${section}[\\s>][\\s\\S]*?</${section}>`))?.[0] || "";
  const keys = [];
  for (const match of block.matchAll(new RegExp(`<${tag}\\b([^>]*)/?>`, "g"))) {
    const attributes = Object.fromEntries([...match[1].matchAll(/(\w+)="([^"]*)"/g)].map((m) => [m[1], m[2]]));
    if (attributes.Time) keys.push({ time: hours(attributes.Time), ...attributes });
  }
  return keys.sort((a, b) => a.time - b.time);
}

// The two keys around time (wrapping past midnight) and how far between them it is.
function around(keys, time) {
  if (!keys.length) return null;
  let before = keys[keys.length - 1], after = keys[0];
  for (let i = 0; i < keys.length; i++) {
    if (keys[i].time <= time) before = keys[i];
    if (keys[i].time > time) { after = keys[i]; break; }
    after = keys[(i + 1) % keys.length];
  }
  const span = (after.time - before.time + 24) % 24 || 24;
  const t = ((time - before.time + 24) % 24) / span;
  return { before, after, t: Math.min(Math.max(t, 0), 1) };
}

function lerpNumbers(keys, time, names) {
  const pair = around(keys, time);
  if (!pair) return null;
  return names.map((name) => {
    const a = parseFloat(pair.before[name] ?? 0), b = parseFloat(pair.after[name] ?? a);
    return a + (b - a) * pair.t;
  });
}

export function parseSky(xml) {
  return {
    sunColour: track(xml, "SunLight", "Color").filter((k) => k.R !== undefined),
    sunDirection: track(xml, "SunLight", "Direction"),
    fogColour: track(xml, "Fog", "Color"),
    fogRange: track(xml, "Fog", "Range"),
    ambient: track(xml, "Ambient", "Texture"),
  };
}

export function sampleSky(sky, time) {
  const sun = lerpNumbers(sky.sunColour, time, ["R", "G", "B", "Brightness"]);
  const direction = lerpNumbers(sky.sunDirection, time, ["Heading", "Pitch"]);
  const fog = lerpNumbers(sky.fogColour, time, ["R", "G", "B"]);
  const range = lerpNumbers(sky.fogRange, time, ["Near", "Far"]);
  const ambient = around(sky.ambient, time);
  return {
    sun: sun && { rgb: sun.slice(0, 3).map((v) => v / 255), brightness: sun[3] },
    direction: direction && { heading: direction[0], pitch: direction[1] },
    fog: fog && fog.map((v) => v / 255),
    range: range && { near: range[0], far: range[1] },
    ambient: ambient && {
      file: (ambient.t < 0.5 ? ambient.before : ambient.after).File,
      brightness: parseFloat(ambient.before.Brightness ?? 1) * (1 - ambient.t) + parseFloat(ambient.after.Brightness ?? 1) * ambient.t,
    },
  };
}
