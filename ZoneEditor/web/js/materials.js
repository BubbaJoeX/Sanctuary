// Forgelight materials in the viewport, drawn with the game's own shader math (Client/Shaders/*_def.fxo) patched
// into three's Lambert and Phong materials: the cut-off of *AlphaTest shaders, glow (light of the surface's own
// colour, masked by the texture's alpha or a GlowMap), specular masked the same way, the material's own tint
// (TintSemantic; TintMask and DualTexture materials tint only where their mask says), scrolling UVs, the Diffuse2
// layer of DualTexture materials on the second UV set, water's three drifting wave maps, a waterfall's two
// scrolling layers and masks, and light beams. A mesh's base (models.py: texture, alpha, shader, params, maps,
// overlay, mask) says which; see the README's "How it works" for where the numbers come from.
import * as THREE from "three";
import { loadTexture } from "./textures.js";

// Seconds, advanced by the frame loop: what scrolling UVs, water and waterfalls move with.
export const shaderTime = { value: 0 };

// The game's *AlphaTest shaders drop pixels whose texture alpha is below this (texkill(a - 0.78)).
const ALPHA_TEST = 0.78;
// Specular strength for Spec materials that don't set one.
const DEFAULT_SPEC = 0.5;
// Water is drawn over what's under it rather than refracting it, so its tint only leans the colour.
const WATER_COLOUR = new THREE.Color(0x2b7d9c);
const WATER_TINT_SHARE = 0.3;

function param(params, name, fallback) {
  const value = params?.[name];
  return value === undefined || value === null ? fallback : value;
}

function srgb(rgb) {
  return new THREE.Color().setRGB(rgb[0], rgb[1], rgb[2], THREE.SRGBColorSpace);
}

// Adds declarations after three's common chunk and code after (or instead of) its other chunks.
function patchShader(source, declarations, edits) {
  let out = source.replace("#include <common>", `#include <common>\n${declarations}`);
  for (const [chunk, code, replace] of edits) {
    out = out.replace(`#include <${chunk}>`, replace ? code : `#include <${chunk}>\n${code}`);
  }
  return out;
}

// One onBeforeCompile per material, with the cache key three needs to keep its variants apart.
function patch(material, key, build) {
  material.customProgramCacheKey = () => key;
  material.onBeforeCompile = build;
  return material;
}

// The material a mesh draws with. texture is the colour texture (a texture variant's, else the mesh's own); tint
// ([r, g, b], 0..1) an object's tint alias colour, which takes the place of the material's own tint.
export function forgelightMaterial(base, texture = base.texture, tint = null) {
  const shader = (base.shader || "").toLowerCase();
  if (shader.startsWith("waterfall") && base.maps?.Texture1) return waterfallMaterial(base);
  if (base.alpha === "water") return waterMaterial(base);
  if (shader.startsWith("lightbeam")) return lightBeamMaterial(base, texture, tint);

  const params = base.params || {};
  const maps = base.maps || {};
  const own = params.TintSemantic;
  const colour = tint ?? (own && own.slice(0, 3).some((v) => Math.abs(v - 1) > 1e-3) ? own : null);
  // TintMask and DualTexture materials tint only where their mask (a TintMask texture, else the diffuse's alpha)
  // says; the rest tint all over.
  const masked = shader.startsWith("tintmask") || shader.startsWith("dualtexture");
  const glow = param(params, "Glow", 0) / 4;
  const spec = shader.includes("spec") ? param(params, "Spec", DEFAULT_SPEC) : 0;
  const scroll = [param(params, "ScrollU", 0), param(params, "ScrollV", 0)];
  const doubleSided = "DoubleSided" in params ? !!params.DoubleSided
    : "DoubleSidedDefaultFalse" in params ? !!params.DoubleSidedDefaultFalse : !!base.alpha;
  const blend = base.alpha === "blend";

  const Material = spec ? THREE.MeshPhongMaterial : THREE.MeshLambertMaterial;
  const material = new Material({
    color: texture ? 0xffffff : 0xb0b0b0, side: doubleSided ? THREE.DoubleSide : THREE.FrontSide,
    alphaTest: base.alpha === "test" ? ALPHA_TEST : 0, transparent: blend, opacity: blend ? 0.75 : 1, depthWrite: !blend,
    map: texture ? loadTexture(texture) : null,
    ...(spec ? { specular: 0xffffff, shininess: 24 } : {}),
  });
  if (colour && !masked) material.color.copy(srgb(colour));
  if (!texture) return material;

  // What's patched in, and the masks it reads.
  const textures = {};
  const glowMask = !glow ? null : shader.startsWith("glow") && maps.GlowMap ? "flGlowMap" : "alpha";
  if (glowMask === "flGlowMap") textures.flGlowMap = loadTexture(maps.GlowMap, { srgb: false });
  const specMask = !spec ? null : maps.SpecularMap ? "flSpecMap" : "alpha";
  if (specMask === "flSpecMap") textures.flSpecMap = loadTexture(maps.SpecularMap, { srgb: false });
  const tintMask = !(masked && colour) ? null : base.mask ? "flMaskMap" : "alpha";
  if (tintMask === "flMaskMap") textures.flMaskMap = loadTexture(base.mask, { srgb: false });
  const overlay = shader.startsWith("dualtexture") && base.overlay && base.uv2;
  if (overlay) textures.flOverlay = loadTexture(base.overlay);
  const scrolls = scroll.some((v) => v);
  if (!glowMask && !specMask && !tintMask && !overlay && !scrolls) return material;

  const sample = (mask) => (mask === "alpha" ? "flAlpha" : `texture2D(${mask}, vMapUv).r`);
  const uniforms = {
    flTime: shaderTime, flScroll: { value: new THREE.Vector2(...scroll) }, flGlow: { value: glow }, flSpec: { value: spec },
    flTint: { value: colour ? srgb(colour) : new THREE.Color(1, 1, 1) },
  };
  const key = `fl|${glowMask}|${specMask}|${tintMask}|${overlay ? 1 : 0}|${scrolls ? 1 : 0}`;
  return patch(material, key, (program) => {
    Object.assign(program.uniforms, uniforms);
    for (const [name, value] of Object.entries(textures)) program.uniforms[name] = { value };
    program.vertexShader = patchShader(program.vertexShader,
      `uniform float flTime;\nuniform vec2 flScroll;\n${overlay ? "attribute vec2 uvOverlay;\nvarying vec2 vOverlayUv;" : ""}`, [
        // The game adds frac(time * scroll) to the UVs.
        ...(scrolls ? [["uv_vertex", "#ifdef USE_MAP\n\tvMapUv += fract(flTime * flScroll);\n#endif"]] : []),
        ...(overlay ? [["uv_vertex", "\tvOverlayUv = uvOverlay;"]] : []),
      ]);
    program.fragmentShader = patchShader(program.fragmentShader,
      ["uniform float flGlow;", "uniform float flSpec;", "uniform vec3 flTint;", ...Object.keys(textures).map((t) => `uniform sampler2D ${t};`),
        overlay ? "varying vec2 vOverlayUv;" : ""].join("\n"), [
        ["map_fragment", [
          "#ifdef USE_MAP\n\tfloat flAlpha = sampledDiffuseColor.a;\n#else\n\tfloat flAlpha = 1.0;\n#endif",
          tintMask ? `\tdiffuseColor.rgb = mix(diffuseColor.rgb, diffuseColor.rgb * flTint, ${sample(tintMask)});` : "",
          overlay ? "\tvec4 flOver = texture2D(flOverlay, vOverlayUv);\n\tdiffuseColor.rgb = mix(diffuseColor.rgb, flOver.rgb, flOver.a);" : "",
        ].join("\n")],
        // Glow is light of the surface's own colour, Glow/4 strong where its mask is white.
        ...(glowMask ? [["emissivemap_fragment", `\ttotalEmissiveRadiance += diffuseColor.rgb * flGlow * ${sample(glowMask)};`]] : []),
        ...(specMask ? [["specularmap_fragment", `\tfloat specularStrength = flSpec * ${sample(specMask)};`, true]] : []),
      ]);
  });
}

// A light beam (a stage light's cone) is light, not a surface: LightBeamSkin_3 multiplies the client's light buffer,
// which holds exp(-light), by exp(-beam), adding the beam to the light the screen is tone-mapped from (about one for
// one near black). The beam is its texture times its tint and Intensity, times the texture's alpha and Density,
// fading toward the cone's outline as (N.V)^FallOff, in stored (gamma) colours; only the inside of the cone is drawn
// (it culls front faces). Here it's added to the screen's colours as they are.
function lightBeamMaterial(base, texture, tint) {
  const params = base.params || {};
  const material = new THREE.MeshLambertMaterial({
    color: srgb(tint ?? params.TintSemantic ?? [1, 1, 1]), map: texture ? loadTexture(texture) : null, side: THREE.BackSide,
    transparent: true, depthWrite: false, fog: false, blending: THREE.AdditiveBlending,
  });
  const uniforms = {
    flStrength: { value: param(params, "Intensity", 1) * param(params, "Density", 1) },
    flFallOff: { value: param(params, "FallOff", 1) },
  };
  return patch(material, "fl-beam", (program) => {
    Object.assign(program.uniforms, uniforms);
    program.fragmentShader = patchShader(program.fragmentShader, "uniform float flStrength;\nuniform float flFallOff;", [
      ["opaque_fragment", [
        "\tfloat flEdge = pow(clamp(abs(dot(normal, normalize(vViewPosition))), 0.0, 1.0), flFallOff);",
        // Worked out in gamma and handed back as linear, which the output encoding turns into gamma again.
        "\tvec3 flBeam = pow(diffuseColor.rgb, vec3(1.0 / 2.2)) * diffuseColor.a * flStrength * flEdge;",
        "\tgl_FragColor = vec4(pow(flBeam, vec3(2.2)), 1.0);",
      ].join("\n"), true],
    ]);
  });
}

// Water: its tint over a sea colour, lit through three wave normal maps that drift over the world (each at its own
// scale and speed, as Water_Def does), and more opaque at a glancing angle.
function waterMaterial(base) {
  const params = base.params || {};
  const maps = base.maps || {};
  const tint = params.TintSemantic;
  const colour = WATER_COLOUR.clone();
  if (tint) colour.lerp(srgb(tint), WATER_TINT_SHARE);
  const material = new THREE.MeshPhongMaterial({
    color: colour, specular: 0xbfe6ff, shininess: 90, transparent: true, opacity: 0.62, depthWrite: false, side: THREE.DoubleSide,
  });
  const waves = [1, 2, 3].filter((i) => maps[`BumpMap${i}`]);
  if (!waves.length) return material;

  const uniforms = { flTime: shaderTime, flFresnel: { value: param(params, "Fresnel", 0.7) } };
  for (const i of waves) {
    uniforms[`flWave${i}`] = { value: loadTexture(maps[`BumpMap${i}`], { srgb: false }) };
    uniforms[`flWaveScale${i}`] = { value: param(params, `TexScale${i}`, 0.02) };
    uniforms[`flWaveScroll${i}`] = { value: new THREE.Vector2(param(params, `TexScrollX${i}`, 0), param(params, `TexScrollZ${i}`, 0)) };
    uniforms[`flWaveBump${i}`] = { value: param(params, `Bumpiness${i}`, 0.3) };
  }
  return patch(material, `fl-water|${waves.join("")}`, (program) => {
    Object.assign(program.uniforms, uniforms);
    program.vertexShader = patchShader(program.vertexShader, "varying vec3 vFlWorld;", [
      ["project_vertex", "\tvFlWorld = (modelMatrix * vec4(transformed, 1.0)).xyz;"],
    ]);
    program.fragmentShader = patchShader(program.fragmentShader, [
      "uniform float flTime;", "uniform float flFresnel;", "varying vec3 vFlWorld;",
      ...waves.flatMap((i) => [`uniform sampler2D flWave${i};`, `uniform float flWaveScale${i};`, `uniform vec2 flWaveScroll${i};`, `uniform float flWaveBump${i};`]),
    ].join("\n"), [
      ["normal_fragment_maps", [
        "\tvec2 flWave = vec2(0.0);",
        ...waves.map((i) => `\tflWave += (texture2D(flWave${i}, vFlWorld.xz * flWaveScale${i} + flTime * flWaveScroll${i}).xy * 2.0 - 1.0) * flWaveBump${i};`),
        "\tnormal = normalize((viewMatrix * vec4(normalize(vec3(flWave.x, 1.0, flWave.y)), 0.0)).xyz);",
      ].join("\n")],
      ["opaque_fragment", "\tgl_FragColor.a = mix(opacity, 0.95, pow(1.0 - clamp(dot(normal, normalize(vViewPosition)), 0.0, 1.0), 3.0) * flFresnel);"],
    ]);
  });
}

// A waterfall: two layers of its texture sliding down at their own scales and speeds (and apart sideways by its
// divergence), averaged and cut by two sliding masks, as Waterfall_Def does.
function waterfallMaterial(base) {
  const params = base.params || {};
  const maps = base.maps || {};
  const glow = param(params, "Glow", 0) / 4;
  const material = new THREE.MeshLambertMaterial({
    color: 0xffffff, transparent: true, depthWrite: false, side: THREE.DoubleSide, map: loadTexture(maps.Texture1),
  });
  const textures = {
    flLayer2: loadTexture(maps.Texture2 || maps.Texture1),
    flMask1: loadTexture(maps.Mask1 || maps.Texture1, { srgb: false }),
    flMask2: loadTexture(maps.Mask2 || maps.Mask1 || maps.Texture1, { srgb: false }),
  };
  const uniforms = {
    flTime: shaderTime, flGlow: { value: glow }, flDivergence: { value: param(params, "Divergence", 0) },
    flScale: { value: new THREE.Vector2(param(params, "TexScale1", 1), param(params, "TexScale2", 1)) },
    flScroll: { value: new THREE.Vector2(param(params, "TexScroll1", 0), param(params, "TexScroll2", 0)) },
    flMaskScale: { value: new THREE.Vector2(param(params, "MaskScale1", 1), param(params, "MaskScale2", 1)) },
    flMaskScroll: { value: new THREE.Vector2(param(params, "MaskScroll1", 0), param(params, "MaskScroll2", 0)) },
  };
  return patch(material, "fl-waterfall", (program) => {
    Object.assign(program.uniforms, uniforms);
    for (const [name, value] of Object.entries(textures)) program.uniforms[name] = { value };
    program.vertexShader = patchShader(program.vertexShader, "varying vec2 vFlUv;", [["uv_vertex", "\tvFlUv = uv;"]]);
    program.fragmentShader = patchShader(program.fragmentShader, [
      "uniform float flTime;", "uniform float flGlow;", "uniform float flDivergence;", "uniform vec2 flScale;", "uniform vec2 flScroll;",
      "uniform vec2 flMaskScale;", "uniform vec2 flMaskScroll;", "uniform sampler2D flLayer2;", "uniform sampler2D flMask1;",
      "uniform sampler2D flMask2;", "varying vec2 vFlUv;",
      // Each layer: u scaled and pushed sideways (the second the other way), v scaled and slid.
      "vec2 flSlide(float scale, float scroll, float side) {",
      "\treturn vec2(vFlUv.x * scale + side * flTime * scroll * flDivergence, vFlUv.y * scale + flTime * scroll);",
      "}",
    ].join("\n"), [
      ["map_fragment", [
        "\tvec4 flA = texture2D(map, flSlide(flScale.x, flScroll.x, 1.0));",
        "\tvec4 flB = texture2D(flLayer2, flSlide(flScale.y, flScroll.y, -1.0));",
        "\tfloat flMask = texture2D(flMask1, flSlide(flMaskScale.x, flMaskScroll.x, 1.0)).r * texture2D(flMask2, flSlide(flMaskScale.y, flMaskScroll.y, -1.0)).r;",
        "\tdiffuseColor = vec4(diffuse * (flA.rgb + flB.rgb) * 0.5, opacity * (flA.a + flB.a) * 0.5 * flMask);",
      ].join("\n"), true],
      ["emissivemap_fragment", "\ttotalEmissiveRadiance += diffuseColor.rgb * flGlow;"],
    ]);
  });
}
