// Fallback catalog for the mock: plausible layers covering every control kind, used whenever the
// mock host has no real layer with that id (other workstreams write the real ones in parallel).
// Shapes follow contract.ts plus the proposed `default` on controls and preset `values`.
import { Control, LayerType, RecipeInfo, SettingValue } from "../../src/contract";

export type MockControl = Control & { default?: SettingValue };
export type MockLayer = Omit<LayerType, "controls" | "presets"> & {
    controls: MockControl[];
    presets: { id: string; name: string; icon?: string; values?: Record<string, SettingValue> }[];
    minArea?: number;
    /** Typed marker kinds (StepView.markerKinds) and how many markers Build needs (StepView.markersRequired). */
    markerKinds?: { kind: string; label: string; icon?: string; tooltip?: string }[];
    markersRequired?: number;
    real?: boolean;
    /** Built-in features (the mock host sends Core's IHasFeatures; fake layers use FAKE_FEATURES). */
    features?: FeatureDef[];
};

const opt = (value: string, label: string, icon?: string, tooltip?: string) => ({ value, label, icon, tooltip });

export const FAKE_LAYERS: MockLayer[] = [
    {
        id: "park", name: "FlowerPainter.Park.Name", icon: "park", kind: "fill", accepts: [], emits: ["green", "core"],
        controls: [
            { id: "style", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Park.Style", tooltip: "FlowerPainter.Park.StyleTip",
              options: [opt("formal", "FlowerPainter.Park.Style.Formal", "plaza"), opt("natural", "FlowerPainter.Park.Style.Natural", "leaf"), opt("meadow", "FlowerPainter.Park.Style.Meadow", "tree")], default: "natural" },
            { id: "pathWidth", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Park.PathWidth", min: 2, max: 8, step: 0.5, unit: "m", default: 4, cheap: false },
            { id: "trees", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Park.TreeDensity", tooltip: "FlowerPainter.Park.TreeDensityTip", min: 0, max: 100, step: 5, unit: "%", default: 60, cheap: true },
            { id: "furniture", kind: "toggle", tier: "essential", group: "", label: "FlowerPainter.Park.Furniture", default: true, cheap: true },
            { id: "pathAsset", kind: "asset", tier: "more", group: "paths", label: "FlowerPainter.Park.PathAsset", assetCategory: "net.pathway", default: "" },
            { id: "treeSpecies", kind: "assetMulti", tier: "more", group: "planting", label: "FlowerPainter.Park.TreeSpecies", assetCategory: "tree", default: [] },
            { id: "entrances", kind: "markers", tier: "more", group: "paths", label: "FlowerPainter.Park.Entrances", markerKinds: ["entrance"] },
            { id: "lake", kind: "toggle", tier: "more", group: "water", label: "FlowerPainter.Park.Lake", tooltip: "FlowerPainter.Park.LakeTip", default: false },
            { id: "lakeSize", kind: "slider", tier: "more", group: "water", label: "FlowerPainter.Park.LakeSize", min: 10, max: 40, step: 5, unit: "%", default: 20, visibleWhen: "lake=true" },
            { id: "fence", kind: "toggle", tier: "more", group: "edge", label: "FlowerPainter.Park.Fence", default: false },
            { id: "plazaShape", kind: "choice", tier: "more", group: "plaza", label: "FlowerPainter.Park.PlazaShape",
              options: [opt("circle", "FlowerPainter.Shape.Circle"), opt("square", "FlowerPainter.Shape.Square"), opt("rectangle", "FlowerPainter.Shape.Rect")], default: "circle" },
            { id: "plazaAt", kind: "choice", tier: "more", group: "plaza", label: "FlowerPainter.Park.PlazaAt",
              options: [opt("centre", "FlowerPainter.At.Centre"), opt("corner", "FlowerPainter.At.Corner"), opt("edge", "FlowerPainter.At.Edge"), opt("entrance", "FlowerPainter.At.Entrance")], default: "centre" },
            { id: "plazaSize", kind: "slider", tier: "more", group: "plaza", label: "FlowerPainter.Park.PlazaSize", min: 5, max: 40, step: 1, unit: "%", default: 15 },
            { id: "fenceAsset", kind: "asset", tier: "more", group: "edge", label: "FlowerPainter.Park.FenceAsset", assetCategory: "fence", default: "", visibleWhen: "fence=true" },
            { id: "ground", kind: "asset", tier: "more", group: "ground", label: "FlowerPainter.Park.Ground", tooltip: "FlowerPainter.Park.GroundTip", assetCategory: "surface.grass", default: "" },
            { id: "decals", kind: "assetMulti", tier: "more", group: "ground", label: "FlowerPainter.Decals.Pick", tooltip: "FlowerPainter.Decals.PickTip", assetCategory: "decal", default: [] },
            { id: "decalDensity", kind: "slider", tier: "more", group: "ground", label: "FlowerPainter.Decals.Density", min: 0, max: 100, step: 5, unit: "%", default: 30 },
            { id: "decalClustering", kind: "slider", tier: "more", group: "ground", label: "FlowerPainter.Decals.Clustering", tooltip: "FlowerPainter.Decals.ClusteringTip", min: 0, max: 100, step: 5, unit: "%", default: 50 },
        ],
        presets: [
            { id: "formal", name: "FlowerPainter.Park.Preset.Formal", values: { style: "formal", pathWidth: 5, trees: 40, furniture: true } },
            { id: "wild", name: "FlowerPainter.Park.Preset.Wild", values: { style: "meadow", pathWidth: 2.5, trees: 85, furniture: false } },
            { id: "pond", name: "FlowerPainter.Park.Preset.Pond", values: { style: "natural", trees: 55, lake: true, lakeSize: 25 } },
        ],
        minArea: 400,
    },
    {
        id: "plaza", name: "FlowerPainter.Plaza.Name", icon: "plaza", kind: "fill", accepts: [], emits: ["paved", "core", "edge"],
        controls: [
            { id: "pattern", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Plaza.Pattern",
              options: [opt("grid", "FlowerPainter.Plaza.Pattern.Grid", "block"), opt("radial", "FlowerPainter.Plaza.Pattern.Radial", "fountain"), opt("open", "FlowerPainter.Plaza.Pattern.Open", "area")], default: "radial" },
            { id: "centerpiece", kind: "asset", tier: "essential", group: "", label: "FlowerPainter.Plaza.Centerpiece", assetCategory: "centerpiece", default: "" },
            { id: "furniture", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Plaza.Furniture", min: 0, max: 100, step: 10, unit: "%", default: 50 },
            { id: "surface", kind: "asset", tier: "more", group: "", label: "FlowerPainter.Plaza.Surface", assetCategory: "surface.paving", default: "" },
            { id: "arrangement", kind: "arrangement", tier: "more", group: "", label: "FlowerPainter.Plaza.Arrangement", assetCategory: "bench", default: ["bench-wood", "lamp-classic", "bench-wood", "planter-square"] },
            { id: "edgeTrees", kind: "toggle", tier: "more", group: "", label: "FlowerPainter.Plaza.EdgeTrees", default: true },
            { id: "decals", kind: "assetMulti", tier: "more", group: "decals", label: "FlowerPainter.Decals.Pick", tooltip: "FlowerPainter.Decals.PickTip", assetCategory: "decal", default: [] },
            { id: "decalDensity", kind: "slider", tier: "more", group: "decals", label: "FlowerPainter.Decals.Density", min: 0, max: 100, step: 5, unit: "%", default: 30 },
            { id: "decalClustering", kind: "slider", tier: "more", group: "decals", label: "FlowerPainter.Decals.Clustering", tooltip: "FlowerPainter.Decals.ClusteringTip", min: 0, max: 100, step: 5, unit: "%", default: 50 },
        ],
        presets: [
            { id: "market", name: "FlowerPainter.Plaza.Preset.Market", values: { pattern: "open", furniture: 30 } },
            { id: "civic", name: "FlowerPainter.Plaza.Preset.Civic", values: { pattern: "radial", furniture: 60, edgeTrees: true } },
        ],
        minArea: 200,
    },
    {
        id: "parking", name: "FlowerPainter.Parking.Name", icon: "parking", kind: "fill", accepts: [], emits: ["lot", "edge"],
        controls: [
            { id: "angle", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Parking.StallAngle",
              options: [opt("90", "FlowerPainter.Parking.Angle90"), opt("60", "FlowerPainter.Parking.Angle60"), opt("45", "FlowerPainter.Parking.Angle45")], default: "90" },
            { id: "entrances", kind: "markers", tier: "essential", group: "", label: "FlowerPainter.Parking.Entrances", markerKinds: ["entrance", "entry", "exit", "alley", "footpath"] },
            { id: "surface", kind: "asset", tier: "essential", group: "", label: "FlowerPainter.Parking.Surface", assetCategory: "surface.asphalt", default: "" },
            { id: "greenery", kind: "toggle", tier: "essential", group: "", label: "FlowerPainter.Parking.Greenery", default: true },
            { id: "aisle", kind: "slider", tier: "more", group: "layout", label: "FlowerPainter.Parking.AisleWidth", min: 5, max: 8, step: 0.5, unit: "m", default: 6 },
            { id: "medians", kind: "toggle", tier: "more", group: "layout", label: "FlowerPainter.Parking.Medians", default: false },
            { id: "lanterns", kind: "toggle", tier: "more", group: "extras", label: "FlowerPainter.Parking.Lanterns", default: true },
            { id: "perimeterRoad", kind: "toggle", tier: "more", group: "layout", label: "FlowerPainter.Parking.PerimeterRoad", default: false },
            { id: "zoningEdges", kind: "toggle", tier: "more", group: "zoning", label: "FlowerPainter.Parking.ZoningEdges", tooltip: "FlowerPainter.Parking.ZoningEdgesTip", default: false },
            { id: "zone", kind: "choice", tier: "more", group: "zoning", label: "FlowerPainter.EdgeZoning.Zone", visibleWhen: "zoningEdges=true",
              options: [opt("resLow", "FlowerPainter.Zone.ResidentialLow"), opt("commercial", "FlowerPainter.Zone.Commercial"), opt("mixed", "FlowerPainter.Zone.Mixed")], default: "commercial" },
            { id: "chargers", kind: "slider", tier: "more", group: "extras", label: "FlowerPainter.Parking.Chargers", min: 0, max: 20, step: 1, unit: "count", default: 0 },
            { id: "fee", kind: "slider", tier: "more", group: "extras", label: "FlowerPainter.Parking.Fee", min: 0, max: 20, step: 1, unit: "money", default: 5 },
        ],
        presets: [],
        minArea: 600,
        markerKinds: [
            { kind: "entrance", label: "FlowerPainter.Marker.TwoWay" },
            { kind: "entry", label: "FlowerPainter.Marker.Entry" },
            { kind: "exit", label: "FlowerPainter.Marker.Exit" },
            { kind: "alley", label: "FlowerPainter.Marker.Alley" },
            { kind: "footpath", label: "FlowerPainter.Marker.Footpath" },
        ],
        markersRequired: 1,
    },
    {
        id: "roads", name: "FlowerPainter.Roads.Name", icon: "roads", kind: "fill", accepts: [], emits: ["block", "edge"],
        controls: [
            { id: "pattern", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Roads.Pattern",
              options: [opt("grid", "FlowerPainter.Roads.Pattern.Grid", "roads"), opt("loop", "FlowerPainter.Roads.Pattern.Loop", "perimeter"), opt("organic", "FlowerPainter.Roads.Pattern.Organic", "path")], default: "grid" },
            { id: "blockSize", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Roads.BlockSize", min: 50, max: 200, step: 5, unit: "m", default: 90 },
            { id: "street", kind: "asset", tier: "essential", group: "", label: "FlowerPainter.Roads.Street", assetCategory: "net.road", default: "" },
            { id: "angle", kind: "slider", tier: "more", group: "", label: "FlowerPainter.Roads.Angle", min: -45, max: 45, step: 1, unit: "deg", default: 0 },
            { id: "perimeter", kind: "toggle", tier: "more", group: "", label: "FlowerPainter.Roads.Perimeter", default: true },
            { id: "culDeSacs", kind: "toggle", tier: "more", group: "", label: "FlowerPainter.Roads.CulDeSacs", default: false },
        ],
        presets: [
            { id: "dense", name: "FlowerPainter.Roads.Preset.Dense", values: { blockSize: 70 } },
            { id: "suburb", name: "FlowerPainter.Roads.Preset.Suburb", values: { blockSize: 140, culDeSacs: true } },
        ],
        minArea: 4000,
    },
    {
        id: "split", hidden: true, name: "FlowerPainter.Split.Name", icon: "split", kind: "split", accepts: [], emits: ["part"],
        controls: [
            { id: "ratio", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Split.Ratio", min: 20, max: 80, step: 5, unit: "%", default: 50 },
            { id: "direction", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Split.Direction",
              options: [opt("vertical", "FlowerPainter.Split.Vertical"), opt("horizontal", "FlowerPainter.Split.Horizontal")], default: "vertical" },
            { id: "divider", kind: "toggle", tier: "more", group: "", label: "FlowerPainter.Split.Divider", default: true },
        ],
        presets: [],
    },
    {
        id: "campground", name: "FlowerPainter.Campground.Name", icon: "campground", kind: "fill", accepts: [], emits: ["green"],
        controls: [
            { id: "pitches", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Campground.Pitches", min: 4, max: 40, step: 1, unit: "count", default: 14 },
            { id: "campfires", kind: "toggle", tier: "essential", group: "", label: "FlowerPainter.Campground.Campfires", default: true },
        ],
        presets: [],
        minArea: 1500,
    },
    {
        id: "playground", name: "FlowerPainter.Playground.Name", icon: "playground", kind: "addOn", accepts: ["green", "core", "root", "block", "part", "paved"], emits: [],
        controls: [
            { id: "size", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.Playground.Size",
              options: [opt("s", "FlowerPainter.Size.Small"), opt("m", "FlowerPainter.Size.Medium"), opt("l", "FlowerPainter.Size.Large")], default: "m" },
            { id: "equipment", kind: "assetMulti", tier: "more", group: "", label: "FlowerPainter.Playground.Equipment", assetCategory: "playground", default: [] },
        ],
        presets: [],
    },
    {
        id: "fountain", name: "FlowerPainter.Fountain.Name", icon: "fountain", kind: "addOn", accepts: ["paved", "core", "green"], emits: [],
        controls: [{ id: "asset", kind: "asset", tier: "essential", group: "", label: "FlowerPainter.Fountain.Asset", assetCategory: "centerpiece", default: "" }],
        presets: [],
    },
    {
        id: "restrooms", name: "FlowerPainter.Restrooms.Name", icon: "restrooms", kind: "addOn", accepts: ["green", "paved", "core"], emits: [],
        controls: [{ id: "count", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Restrooms.Count", min: 1, max: 4, step: 1, unit: "count", default: 1 }],
        presets: [],
    },
    {
        id: "bigTrees", name: "FlowerPainter.BigTrees.Name", icon: "bigTrees", kind: "addOn", accepts: ["green", "edge", "root", "block", "core"], emits: [],
        controls: [
            { id: "count", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.BigTrees.Count", min: 1, max: 30, step: 1, unit: "count", default: 8 },
            { id: "species", kind: "assetMulti", tier: "essential", group: "", label: "FlowerPainter.BigTrees.Species", assetCategory: "tree", default: [] },
        ],
        presets: [],
    },
    {
        id: "edgeZoning", name: "FlowerPainter.EdgeZoning.Name", icon: "edgeZoning", kind: "addOn", accepts: ["edge", "block", "root", "lot", "paved"], emits: [],
        controls: [
            { id: "zone", kind: "choice", tier: "essential", group: "", label: "FlowerPainter.EdgeZoning.Zone",
              options: [opt("resLow", "FlowerPainter.Zone.ResidentialLow"), opt("resHigh", "FlowerPainter.Zone.ResidentialHigh"), opt("commercial", "FlowerPainter.Zone.Commercial"), opt("mixed", "FlowerPainter.Zone.Mixed"), opt("office", "FlowerPainter.Zone.Office"), opt("industry", "FlowerPainter.Zone.Industry")], default: "mixed" },
            { id: "depth", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.EdgeZoning.Depth", min: 1, max: 6, step: 1, unit: "count", default: 4 },
        ],
        presets: [],
    },
    {
        id: "pond", hidden: true, name: "FlowerPainter.Pond.Name", icon: "water", kind: "addOn", accepts: ["green"], emits: [],
        controls: [
            { id: "size", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Pond.Size", tooltip: "FlowerPainter.Pond.SizeTip", min: 5, max: 40, step: 5, unit: "%", default: 15 },
            { id: "shore", kind: "choice", tier: "more", group: "", label: "FlowerPainter.Pond.Shore",
              options: [opt("grass", "FlowerPainter.Pond.Shore.Grass"), opt("stone", "FlowerPainter.Pond.Shore.Stone")], default: "grass" },
        ],
        presets: [],
    },
    {
        id: "kiosk", name: "FlowerPainter.Kiosk.Name", icon: "building", kind: "addOn", accepts: ["paved", "core"], emits: [],
        controls: [{ id: "count", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Kiosk.Count", min: 1, max: 4, step: 1, unit: "count", default: 1 }],
        presets: [],
    },
    {
        id: "picnic", name: "FlowerPainter.Picnic.Name", icon: "picnic", kind: "addOn", accepts: ["green"], emits: [],
        controls: [{ id: "tables", kind: "slider", tier: "essential", group: "", label: "FlowerPainter.Picnic.Tables", min: 2, max: 16, step: 1, unit: "count", default: 6 }],
        presets: [],
    },
];

export interface MockRecipe {
    info: RecipeInfo;
    root: RecipeTemplate;
}

export interface RecipeTemplate {
    layer: string;
    target?: string;
    values?: Record<string, SettingValue>;
    children?: RecipeTemplate[];
    overrides?: Record<string, RecipeTemplate>;
}

/** Built-in features of fill layers (mirrors Core.FeatureInfo): a toggle that adds a child step. */
export interface FeatureDef {
    layer: string;
    target?: string;
    defaultOn?: boolean;
    labelKey?: string;
    tooltip?: string;
    /** Ids of the PARENT's controls that belong to the feature (shown under its toggle). */
    options?: string[];
    preset?: Record<string, SettingValue>;
}

/** Playtest 2 decision: refine each type with features instead of free mixing. */
export const FAKE_FEATURES: Record<string, FeatureDef[]> = {
    park: [
        { layer: "plaza", target: "core", labelKey: "FlowerPainter.Feature.CentralPlaza", tooltip: "FlowerPainter.Feature.CentralPlazaTip", options: ["plazaShape", "plazaAt", "plazaSize"], preset: { pattern: "radial", furniture: 40 } },
        { layer: "playground" },
        { layer: "pond", labelKey: "FlowerPainter.Feature.Pond", tooltip: "FlowerPainter.Pond.SizeTip" },
        { layer: "picnic" },
        { layer: "restrooms" },
        { layer: "fountain" },
        { layer: "bigTrees" },
    ],
    plaza: [
        { layer: "bigTrees", labelKey: "FlowerPainter.Feature.Trees", defaultOn: true },
        { layer: "playground" },
        { layer: "fountain" },
        { layer: "kiosk" },
    ],
    roads: [{ layer: "edgeZoning", target: "edge", labelKey: "FlowerPainter.Feature.StreetZoning" }],
};

export const FAKE_RECIPES: MockRecipe[] = [
    { info: { id: "parking", name: "FlowerPainter.Parking.Name", icon: "parking", custom: false, description: "FlowerPainter.Recipe.ParkingTip", group: "parking" }, root: { layer: "parking" } },
    { info: { id: "plaza", name: "FlowerPainter.Plaza.Name", icon: "plaza", custom: false, description: "FlowerPainter.Recipe.PlazaTip", group: "squares" }, root: { layer: "plaza" } },
    { info: { id: "park", name: "FlowerPainter.Park.Name", icon: "park", custom: false, description: "FlowerPainter.Recipe.ParkTip", group: "parks" }, root: { layer: "park" } },
    { info: { id: "roads", name: "FlowerPainter.Roads.Name", icon: "roads", custom: false, description: "FlowerPainter.Recipe.RoadsTip", group: "roads" }, root: { layer: "roads" } },
    {
        info: { id: "roads-with-parks", name: "FlowerPainter.Recipe.RoadsParks", icon: "roadsParks", custom: false, description: "FlowerPainter.Recipe.RoadsParksTip" },
        root: { layer: "roads", children: [{ layer: "park", target: "block" }] },
    },
    {
        info: { id: "park-plaza", name: "FlowerPainter.Recipe.ParkPlaza", icon: "parkPlaza", custom: false, description: "FlowerPainter.Recipe.ParkPlazaTip" },
        root: { layer: "split", children: [{ layer: "park", target: "part:0" }, { layer: "plaza", target: "part:1" }] },
    },
    {
        info: { id: "neighbourhood-park", name: "FlowerPainter.Recipe.NeighbourhoodPark", icon: "playground", custom: false, description: "FlowerPainter.Recipe.NeighbourhoodParkTip" },
        root: { layer: "park", children: [{ layer: "playground" }, { layer: "restrooms" }] },
    },
    { info: { id: "campground", name: "FlowerPainter.Campground.Name", icon: "campground", custom: false, description: "FlowerPainter.Recipe.CampgroundTip", group: "nature" }, root: { layer: "campground" } },
];

/** English for the fake layers' keys (the real layers ship their own Lang files). */
export const FAKE_STRINGS: Record<string, string> = {
    "FlowerPainter.Park.Name": "Park", "FlowerPainter.Park.Style": "Style", "FlowerPainter.Park.StyleTip": "Overall look: formal lines, natural curves or an open meadow.",
    "FlowerPainter.Park.Style.Formal": "Formal", "FlowerPainter.Park.Style.Natural": "Natural", "FlowerPainter.Park.Style.Meadow": "Meadow",
    "FlowerPainter.Park.PathWidth": "Path width", "FlowerPainter.Park.TreeDensity": "Trees", "FlowerPainter.Park.TreeDensityTip": "How much of the lawn is planted with trees.",
    "FlowerPainter.Park.Furniture": "Benches & lamps", "FlowerPainter.Park.PathAsset": "Path type", "FlowerPainter.Park.TreeSpecies": "Tree species",
    "FlowerPainter.Park.Entrances": "Entrances", "FlowerPainter.Park.Lake": "Pond", "FlowerPainter.Park.LakeTip": "Digs a pond. Terrain changes are permanent.",
    "FlowerPainter.Park.LakeSize": "Pond size", "FlowerPainter.Park.Fence": "Fence", "FlowerPainter.Park.FenceAsset": "Fence type",
    "FlowerPainter.Park.Preset.Formal": "Formal garden", "FlowerPainter.Park.Preset.Wild": "Wild meadow", "FlowerPainter.Park.Preset.Pond": "Pond park",
    "FlowerPainter.Plaza.Name": "Plaza", "FlowerPainter.Plaza.Pattern": "Layout", "FlowerPainter.Plaza.Pattern.Grid": "Grid", "FlowerPainter.Plaza.Pattern.Radial": "Radial",
    "FlowerPainter.Plaza.Pattern.Open": "Open", "FlowerPainter.Plaza.Centerpiece": "Centerpiece", "FlowerPainter.Plaza.Furniture": "Furniture",
    "FlowerPainter.Plaza.Surface": "Paving", "FlowerPainter.Plaza.Arrangement": "Edge sequence", "FlowerPainter.Plaza.EdgeTrees": "Trees along the edge",
    "FlowerPainter.Plaza.Preset.Market": "Market square", "FlowerPainter.Plaza.Preset.Civic": "Civic square",
    "FlowerPainter.Parking.Name": "Parking", "FlowerPainter.Parking.StallAngle": "Stall angle", "FlowerPainter.Parking.Angle90": "90°", "FlowerPainter.Parking.Angle60": "60°",
    "FlowerPainter.Parking.Angle45": "45°", "FlowerPainter.Parking.Entrances": "Entrances", "FlowerPainter.Parking.Surface": "Surface", "FlowerPainter.Parking.Greenery": "Greenery",
    "FlowerPainter.Parking.AisleWidth": "Aisle width", "FlowerPainter.Parking.Medians": "Planted medians", "FlowerPainter.Parking.Lanterns": "Lanterns",
    "FlowerPainter.Parking.Chargers": "EV chargers", "FlowerPainter.Parking.Fee": "Parking fee", "FlowerPainter.Parking.NeedsEntrance": "Place at least one entrance on the outline, where cars come in.",
    "FlowerPainter.Parking.TooSmall": "Too small for a parking lot (at least 600 m²).",
    "FlowerPainter.Roads.Name": "Roads", "FlowerPainter.Roads.Pattern": "Pattern", "FlowerPainter.Roads.Pattern.Grid": "Grid", "FlowerPainter.Roads.Pattern.Loop": "Loops",
    "FlowerPainter.Roads.Pattern.Organic": "Organic", "FlowerPainter.Roads.BlockSize": "Block size", "FlowerPainter.Roads.Street": "Street type", "FlowerPainter.Roads.Angle": "Rotation",
    "FlowerPainter.Roads.Perimeter": "Road along the edge", "FlowerPainter.Roads.CulDeSacs": "Cul-de-sacs", "FlowerPainter.Roads.Preset.Dense": "Dense grid", "FlowerPainter.Roads.Preset.Suburb": "Suburb",
    "FlowerPainter.Split.Name": "Split", "FlowerPainter.Split.Ratio": "First part", "FlowerPainter.Split.Direction": "Direction", "FlowerPainter.Split.Vertical": "Side by side",
    "FlowerPainter.Split.Horizontal": "Stacked", "FlowerPainter.Split.Divider": "Path between parts",
    "FlowerPainter.Campground.Name": "Campground", "FlowerPainter.Campground.Pitches": "Pitches", "FlowerPainter.Campground.Campfires": "Campfires",
    "FlowerPainter.Playground.Name": "Playground", "FlowerPainter.Playground.Size": "Size", "FlowerPainter.Playground.Equipment": "Equipment",
    "FlowerPainter.Size.Small": "Small", "FlowerPainter.Size.Medium": "Medium", "FlowerPainter.Size.Large": "Large",
    "FlowerPainter.Fountain.Name": "Fountain", "FlowerPainter.Fountain.Asset": "Fountain",
    "FlowerPainter.Restrooms.Name": "Restrooms", "FlowerPainter.Restrooms.Count": "Buildings",
    "FlowerPainter.BigTrees.Name": "Big trees", "FlowerPainter.BigTrees.Count": "Trees", "FlowerPainter.BigTrees.Species": "Species",
    "FlowerPainter.EdgeZoning.Name": "Edge zoning", "FlowerPainter.EdgeZoning.Zone": "Zone", "FlowerPainter.EdgeZoning.Depth": "Depth (cells)",
    "FlowerPainter.Zone.ResidentialLow": "Low res.", "FlowerPainter.Zone.ResidentialHigh": "High res.", "FlowerPainter.Zone.Commercial": "Commercial",
    "FlowerPainter.Zone.Mixed": "Mixed", "FlowerPainter.Zone.Office": "Office", "FlowerPainter.Zone.Industry": "Industry",
    "FlowerPainter.Picnic.Name": "Picnic area", "FlowerPainter.Picnic.Tables": "Tables",
    "FlowerPainter.Pond.Name": "Pond", "FlowerPainter.Pond.Size": "Size", "FlowerPainter.Pond.SizeTip": "Share of the park. Digging is permanent.",
    "FlowerPainter.Pond.Shore": "Shore", "FlowerPainter.Pond.Shore.Grass": "Grass", "FlowerPainter.Pond.Shore.Stone": "Stone",
    "FlowerPainter.Kiosk.Name": "Kiosk", "FlowerPainter.Kiosk.Count": "Kiosks",
    "FlowerPainter.Feature.CentralPlaza": "Central plaza", "FlowerPainter.Feature.Pond": "Pond", "FlowerPainter.Feature.Trees": "Trees",
    "FlowerPainter.Feature.StreetZoning": "Zoning along streets",
    "FlowerPainter.Feature.CentralPlazaTip": "A paved plaza inside the park; pick its shape, place and size.",
    "FlowerPainter.Park.PlazaShape": "Shape", "FlowerPainter.Park.PlazaAt": "Position", "FlowerPainter.Park.PlazaSize": "Size",
    "FlowerPainter.Shape.Circle": "Circle", "FlowerPainter.Shape.Square": "Square", "FlowerPainter.Shape.Rect": "Rect",
    "FlowerPainter.At.Centre": "Centre", "FlowerPainter.At.Corner": "Corner", "FlowerPainter.At.Edge": "Edge", "FlowerPainter.At.Entrance": "Entrance",
    "FlowerPainter.Recipe.RoadsParks": "Roads + parks", "FlowerPainter.Recipe.ParkPlaza": "Park | plaza", "FlowerPainter.Recipe.NeighbourhoodPark": "Family park",
    "FlowerPainter.Marker.TwoWay": "Two-way", "FlowerPainter.Marker.Entry": "Entry only", "FlowerPainter.Marker.Exit": "Exit only",
    "FlowerPainter.Marker.Alley": "Alley", "FlowerPainter.Marker.Footpath": "Footpath", "FlowerPainter.Reason.NeedEntrance": "Place at least one entrance", "FlowerPainter.Reason.AddEntrance": "Add an entrance to build",
    "FlowerPainter.Parking.PerimeterRoad": "Perimeter road", "FlowerPainter.Parking.ZoningEdges": "Zone the edges", "FlowerPainter.Parking.ZoningEdgesTip": "Zones a strip along the lot so buildings can grow next to it.",
    "FlowerPainter.Park.Ground": "Lawn", "FlowerPainter.Park.GroundTip": "Ground texture under the lawn.",
    "FlowerPainter.Decals.Pick": "Decals", "FlowerPainter.Decals.PickTip": "Leaves, cracks, puddles, markings, scattered over the ground.",
    "FlowerPainter.Decals.Density": "Density", "FlowerPainter.Decals.Clustering": "Clustering", "FlowerPainter.Decals.ClusteringTip": "Low: spread evenly. High: in patches.",
    "FlowerPainter.UI.Group.ground": "Ground", "FlowerPainter.UI.Group.decals": "Decals",
    "FlowerPainter.UI.Group.zoning": "Zoning", "FlowerPainter.UI.Group.layout": "Layout", "FlowerPainter.UI.Group.extras": "Extras", "FlowerPainter.UI.Group.paths": "Paths",
    "FlowerPainter.UI.Group.planting": "Planting", "FlowerPainter.UI.Group.water": "Water", "FlowerPainter.UI.Group.edge": "Edge",
    "FlowerPainter.Recipe.ParkingTip": "A surface parking lot with stalls, aisles and entrances.",
    "FlowerPainter.Recipe.PlazaTip": "A paved square with a centrepiece, seating and lights.",
    "FlowerPainter.Recipe.ParkTip": "Paths, lawns, trees and benches.",
    "FlowerPainter.Recipe.RoadsTip": "A street pattern fitted to the area.",
    "FlowerPainter.Recipe.RoadsParksTip": "Streets, and every block between them becomes a park.",
    "FlowerPainter.Recipe.ParkPlazaTip": "Half park, half plaza.",
    "FlowerPainter.Recipe.NeighbourhoodParkTip": "A park with a playground and restrooms.",
    "FlowerPainter.Recipe.CampgroundTip": "Tent pitches around a loop path.",
    "FlowerPainter.Stat.Trees": "trees", "FlowerPainter.Stat.Benches": "benches", "FlowerPainter.Stat.Lamps": "lamps", "FlowerPainter.Stat.Stalls": "stalls",
    "FlowerPainter.Stat.PathLength": "of paths", "FlowerPainter.Stat.RoadLength": "of road", "FlowerPainter.Stat.Blocks": "blocks", "FlowerPainter.Stat.Playgrounds": "playgrounds",
    "FlowerPainter.Stat.Fountains": "fountains", "FlowerPainter.Stat.Restrooms": "restrooms", "FlowerPainter.Stat.ZoneCells": "zone cells", "FlowerPainter.Stat.Tents": "tents",
    "FlowerPainter.Stat.Chargers": "EV chargers", "FlowerPainter.Stat.PavedArea": "paved",
    "FlowerPainter.Issue.PlazaLarge": "Large plazas feel empty. Consider Park | plaza, or more furniture.",
    "FlowerPainter.Issue.TreesSkipped": "{0} trees were left out where they would block a path.",
    "FlowerPainter.Issue.BlockTooSmall": "Block {0} is too small to fill and stays empty.",
    "FlowerPainter.Issue.NoRoom": "{0}: no free spot left, skipped.",
    "FlowerPainter.Marker.entrance.Plural": "entrances",
};

// ───────────── fake assets (fakeAssets.ts) ─────────────

export { fakeAssets, surfaceColor } from "./fakeAssets";

// ───────────── more types (the Make row at scale) ─────────────

/** Types other workstreams are adding: backend.addMoreTypes() puts them in the catalog (Make-row tests). */
export const MORE_TYPES: [string, string, string, string][] = [
    ["botanicalGarden", "Botanical garden", "flower", "gardens"],
    ["sportsPark", "Sports park", "people", "sports"],
    ["dogPark", "Dog park", "park", "parks"],
    ["picnicGrounds", "Picnic grounds", "picnic", "parks"],
    ["natureReserve", "Nature reserve", "tree", "nature"],
    ["formalGarden", "Formal garden", "flower", "gardens"],
    ["communityGarden", "Community garden", "leaf", "gardens"],
    ["marketSquare", "Market square", "plaza", "squares"],
    ["parkingGarage", "Parking garage", "parking", "parking"],
    ["quarry", "Quarry", "area", "industrial"],
    ["restArea", "Rest area", "parking", "transport"],
];
