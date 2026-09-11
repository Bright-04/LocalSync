namespace LocalSync.Core.Security;

/// <summary>
/// A 256-word list for rendering a device id as something a human can compare
/// aloud. Exactly 256 entries so one byte maps to one word with no bias.
/// </summary>
/// <remarks>
/// Words are lowercase, alphabetic, three syllables or fewer, and chosen to be
/// distinguishable when spoken. This list is part of the display contract: once
/// released, reordering it changes every device's mnemonic.
/// </remarks>
internal static class MnemonicWords
{
    internal static readonly string[] Words =
    [
        "amber", "anchor", "apple", "arbor", "arrow", "aspen", "atlas", "autumn",
        "avenue", "azure", "bacon", "badge", "bamboo", "banjo", "barley", "basin",
        "beacon", "beagle", "bearing", "bedrock", "bellow", "birch", "bishop", "bison",
        "blanket", "blossom", "bluff", "bobcat", "bolder", "bonsai", "border", "bottle",
        "boulder", "bracket", "brandy", "bravo", "breeze", "bridge", "bronze", "brook",
        "buffet", "bugle", "bunker", "burrow", "bustle", "cabin", "cactus", "canary",
        "candle", "canopy", "canvas", "canyon", "cargo", "carbon", "carrot", "cascade",
        "castle", "catalog", "cedar", "cellar", "cement", "census", "chalet", "chalk",
        "chamber", "chapel", "charter", "cherry", "chisel", "cider", "cinder", "circus",
        "cistern", "citrus", "clamp", "clarity", "clatter", "cliff", "cobalt", "cocoa",
        "collar", "column", "comet", "compass", "copper", "coral", "cortex", "cosmos",
        "cotton", "cougar", "cove", "crater", "crayon", "crimson", "crystal", "cubic",
        "cumin", "curfew", "current", "cypress", "dagger", "dahlia", "damson", "dapple",
        "dartboard", "dawn", "decoy", "delta", "denim", "derby", "desert", "diamond",
        "digit", "dingo", "dolphin", "domain", "donut", "dorsal", "draft", "dragon",
        "drifter", "dune", "duplex", "dynamo", "eagle", "ebony", "echo", "eclipse",
        "elbow", "ember", "emerald", "empire", "engine", "enigma", "envoy", "epoch",
        "equator", "ermine", "escort", "estate", "ether", "exodus", "fable", "falcon",
        "fathom", "feather", "fedora", "fennel", "ferry", "fiber", "fiddle", "filter",
        "finch", "fjord", "flagon", "flannel", "flint", "florin", "flotilla", "foghorn",
        "forest", "fossil", "fountain", "foxglove", "fragment", "freight", "fresco", "frost",
        "fulcrum", "funnel", "gadget", "galaxy", "gallery", "gambit", "garnet", "gazette",
        "gecko", "geyser", "gilded", "ginger", "glacier", "glider", "granite", "grotto",
        "guitar", "gully", "gusto", "habitat", "halibut", "hammock", "harbor", "harvest",
        "hazel", "heather", "helix", "hemlock", "herald", "hickory", "hollow", "honey",
        "horizon", "hornet", "hostel", "hurdle", "hydrant", "iceberg", "impala", "indigo",
        "inlet", "ivory", "jackal", "jasmine", "jester", "jetty", "jigsaw", "jockey",
        "jubilee", "juniper", "kayak", "kernel", "kestrel", "keystone", "kindle", "kingdom",
        "lagoon", "lancer", "lantern", "lattice", "lavender", "ledger", "legacy", "lemon",
        "lentil", "lever", "lichen", "lilac", "limber", "linen", "lobster", "locket",
        "lotus", "lumber", "lunar", "lyric", "magnet", "mahogany", "mallet", "mammoth",
        "mango", "mantis", "maple", "marble", "mariner", "marlin", "marrow", "marsh",
    ];
}
