using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

using Microsoft.Extensions.Logging;

using Sanctuary.Core.Collections;

using Sanctuary.Game.Resources;
using Sanctuary.Game.Resources.Definitions.Rewards;

namespace Sanctuary.Game;

public class ResourceManager : IResourceManager, IDisposable
{
    private ILogger _logger;
    private FileSystemWatcher _fileSystemWatcher;
    private readonly Dictionary<string, Func<bool>> _reloaders;

    public static readonly string BaseDirectory = "Resources";

    public static readonly string HairMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HairMappings.txt");
    public static readonly string HeadMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "HeadMappings.txt");
    public static readonly string SkinToneMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "SkinToneMappings.txt");
    public static readonly string FacePaintMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "FacePaintMappings.txt");
    public static readonly string ModelCustomizationMappingsFile = Path.Combine(BaseDirectory, "CharacterCreate", "ModelCustomizationMappings.txt");

    public static readonly string ModelsFile = Path.Combine(BaseDirectory, "Models.txt");

    public static readonly string FirstTimeEventsFile = Path.Combine(BaseDirectory, "FirstTimeEvents.txt");
    public static readonly string MiniGameTutorialsFile = Path.Combine(BaseDirectory, "GameTutorialData.txt");

    public static readonly string ClientItemDefinitionsFile = Path.Combine(BaseDirectory, "ClientItemDefinitions.json");
    public static readonly string CollectionsFile = Path.Combine(BaseDirectory, "Collections.json");
    public static readonly string CollectionCoinsFile = Path.Combine(BaseDirectory, "CollectionCoins.json");
    public static readonly string CollectionNodePoolsFile = Path.Combine(BaseDirectory, "CollectionNodePools.json");
    public static readonly string CollectionNodeTypesFile = Path.Combine(BaseDirectory, "CollectionNodeTypes.json");
    public static readonly string CollectionNodeSpawnsDirectory = Path.Combine(BaseDirectory, "CollectionNodeSpawns");

    public static readonly string CoinStoreItemsFile = Path.Combine(BaseDirectory, "CoinStoreItems.json");
    public static readonly string CoinStoreBundlesFile = Path.Combine(BaseDirectory, "CoinStoreBundles.json");

    public static readonly string ItemClassesFile = Path.Combine(BaseDirectory, "ItemClasses.txt");
    public static readonly string EquipmentSlotDefinitionsFile = Path.Combine(BaseDirectory, "EquipmentSlotDefinitions.txt");
    public static readonly string ItemCategoriesFile = Path.Combine(BaseDirectory, "ItemCategories.txt");
    public static readonly string ItemCategoryGroupsFile = Path.Combine(BaseDirectory, "ItemCategoryGroups.txt");


    public static readonly string ZonesDirectory = Path.Combine(BaseDirectory, "Zones");
    public static readonly string ZoneEnvironmentsDirectory = Path.Combine(ZonesDirectory, "Environments");
    public static readonly string HousesFile = Path.Combine(BaseDirectory, "Houses.json");
    public static readonly string HousingFixturesFile = Path.Combine(BaseDirectory, "HousingFixtures.json");
    public static readonly string FishingFile = Path.Combine(BaseDirectory, "Fishing.json");
    public static readonly string ChallengesFile = Path.Combine(BaseDirectory, "Challenges.json");
    public static readonly string BattlesFile = Path.Combine(BaseDirectory, "Battles.json");
    public static readonly string AchievementsFile = Path.Combine(BaseDirectory, "Achievements.json");
    public static readonly string RecipesFile = Path.Combine(BaseDirectory, "Recipes.json");
    public static readonly string StationsFile = Path.Combine(BaseDirectory, "Stations.json");
    public static readonly string GiftsFile = Path.Combine(BaseDirectory, "Gifts.json");
    public static readonly string PetsFile = Path.Combine(BaseDirectory, "Pets.json");
    /// <summary>
    /// The minigames (one .json per game family), with each game's own data in a folder of its name.
    /// </summary>
    public static readonly string MinigamesDirectory = Path.Combine(BaseDirectory, "Minigames");
    public static readonly string MountsFile = Path.Combine(BaseDirectory, "Mounts.json");
    public static readonly string ProfilesFile = Path.Combine(BaseDirectory, "Profiles.json");
    public static readonly string QuickChatsFile = Path.Combine(BaseDirectory, "QuickChats.json");
    public static readonly string PlayerTitlesFile = Path.Combine(BaseDirectory, "PlayerTitles.json");
    public static readonly string PointOfInterestsFile = Path.Combine(BaseDirectory, "PointOfInterests.json");
    public static readonly string SeasonsFile = Path.Combine(BaseDirectory, "Seasons.json");
    public static readonly string ConsumablesFile = Path.Combine(BaseDirectory, "Consumables.json");
    public static readonly string ItemAbilitiesFile = Path.Combine(BaseDirectory, "ItemAbilities.json");
    public static readonly string NpcsFile = Path.Combine(BaseDirectory, "Npcs.json");
    public static readonly string NpcMovementsFile = Path.Combine(BaseDirectory, "NpcMovements.json");
    public static readonly string NpcAmbientFile = Path.Combine(BaseDirectory, "NpcAmbient.json");
    public static readonly string NameFilterFile = Path.Combine(BaseDirectory, "NameFilter.txt");
    public static readonly string QuestsFile = Path.Combine(BaseDirectory, "Quests.json");
    public static readonly string MapsDirectory = Path.Combine(BaseDirectory, "Maps");
    public static readonly string TerrainDirectory = Path.Combine(BaseDirectory, "Terrain");
    public static readonly string NavMeshDirectory = Path.Combine(BaseDirectory, "NavMeshes");
    public static readonly string RewardTablesFile = Path.Combine(BaseDirectory, "Rewards.json");
    public static readonly string CombatAbilitiesFile = Path.Combine(BaseDirectory, "CombatAbilities.json");
    public static readonly string CombatJobsFile = Path.Combine(BaseDirectory, "CombatJobs.json");
    public static readonly string MobsFile = Path.Combine(BaseDirectory, "Mobs.json");


    public IdToStringLookup HairMappings { get; }
    public IdToStringLookup HeadMappings { get; }
    public IdToStringLookup SkinToneMappings { get; }
    public IdToStringLookup FacePaintMappings { get; }
    public IdToStringLookup ModelCustomizationMappings { get; }

    public IdToStringLookup FirstTimeEvents { get; }
    public IdToStringLookup MiniGameTutorials { get; }

    public ModelDefinitionCollection Models { get; }

    public ClientItemDefinitionCollection ClientItemDefinitions { get; }
    public CollectionDefinitionCollection Collections { get; }
    public CollectionCoinDefinitionCollection CollectionCoins { get; }
    public CollectionNodePoolDefinitionCollection CollectionNodePools { get; }
    public CollectionNodeTypeDefinitionCollection CollectionNodeTypes { get; }
    public CollectionNodeSpawnDefinitionCollection CollectionNodeSpawns { get; }

    public CoinStoreItemCollection CoinStoreItems { get; }
    public CoinStoreBundleCollection CoinStoreBundles { get; }

    public ItemClassDefinitionCollection ItemClasses { get; }
    public EquipmentSlotDefinitionCollection EquipmentSlots { get; }
    public ItemCategoryDefinitionCollection ItemCategories { get; }
    public ItemCategoryGroupDefinitionCollection ItemCategoryGroups { get; }


    public ZoneDefinitionCollection Zones { get; }
    public ZoneEnvironmentCollection ZoneEnvironments { get; }
    public HouseDefinitionCollection Houses { get; }
    public HousingFixtureDefinitionCollection HousingFixtures { get; }
    public FishingCollection Fishing { get; }
    public ChallengeDefinitionCollection Challenges { get; }
    public BattleDefinitionCollection Battles { get; }
    public AchievementDefinitionCollection Achievements { get; }
    public RecipeDefinitionCollection Recipes { get; }
    public StationDefinitionCollection Stations { get; }
    public GiftDefinitionCollection Gifts { get; }
    public MountDefinitionCollection Mounts { get; }
    public PetDefinitionCollection Pets { get; }
    public MinigameDefinitionCollection Minigames { get; }
    public PlayerTitleCollection PlayerTitles { get; }
    public ProfileDefinitionCollection Profiles { get; }
    public QuickChatDefinitionCollection QuickChats { get; }
    public PointOfInterestDefinitionCollection PointOfInterests { get; }
    public SeasonDefinitionCollection Seasons { get; }
    public ConsumableCollection Consumables { get; }
    public ItemAbilityDefinitionCollection ItemAbilities { get; }
    public NpcDefinitionCollection Npcs { get; }
    public NameFilterCollection NameFilter { get; }
    public NpcMovementDefinitionCollection NpcMovements { get; }
    public NpcAmbientDefinitionCollection NpcAmbient { get; }
    public QuestDefinitionCollection Quests { get; }
    public MapGraphCollection Maps { get; }
    public TerrainCollection Terrains { get; }
    public NavMeshCollection NavMeshes { get; }

    public RewardTableDefinitionCollection RewardTables { get; }

    public AbilityDefinitionCollection CombatAbilities { get; }
    public JobKitDefinitionCollection CombatJobs { get; }
    public MobDefinitionCollection Mobs { get; }

    public ResourceManager(ILogger<ResourceManager> logger)
    {
        _logger = logger;
        _fileSystemWatcher = new(BaseDirectory)
        {
            IncludeSubdirectories = true
        };

        _fileSystemWatcher.Changed += _fileSystemWatcher_Changed;
        _fileSystemWatcher.EnableRaisingEvents = true;

        HairMappings = new(_logger);
        HeadMappings = new(_logger);
        SkinToneMappings = new(_logger);
        FacePaintMappings = new(_logger);
        ModelCustomizationMappings = new(_logger);

        FirstTimeEvents = new(_logger);
        MiniGameTutorials = new(_logger);

        Models = new(_logger);

        ClientItemDefinitions = new(_logger);
        Collections = new(_logger);
        CollectionCoins = new(_logger);
        CollectionNodePools = new(_logger);
        CollectionNodeTypes = new(_logger);
        CollectionNodeSpawns = new(_logger);

        CoinStoreItems = new(_logger);
        CoinStoreBundles = new(_logger);

        ItemClasses = new(_logger);
        EquipmentSlots = new(_logger);
        ItemCategories = new(_logger);
        ItemCategoryGroups = new(_logger);


        Zones = new(_logger);
        ZoneEnvironments = new(_logger);
        Houses = new(_logger);
        HousingFixtures = new(_logger);
        Fishing = new(_logger);
        Challenges = new(_logger);
        Battles = new(_logger);
        Achievements = new(_logger);
        Recipes = new(_logger);
        Stations = new(_logger);
        Gifts = new(_logger);
        Mounts = new(_logger);
        Pets = new(_logger);
        Minigames = new(_logger);
        Profiles = new(_logger);
        QuickChats = new(_logger);
        PlayerTitles = new(_logger);
        PointOfInterests = new(_logger);
        Seasons = new(_logger);
        Consumables = new(_logger);
        ItemAbilities = new(_logger);
        Npcs = new(_logger);
        NameFilter = new(_logger);
        NpcMovements = new(_logger);
        NpcAmbient = new(_logger);
        Quests = new(_logger);
        Maps = new(_logger);
        Terrains = new(_logger);
        NavMeshes = new(_logger);
        RewardTables = new(_logger);
        CombatAbilities = new(_logger);
        CombatJobs = new(_logger);
        Mobs = new(_logger);

        _reloaders = new(StringComparer.OrdinalIgnoreCase)
        {
            ["models"] = () => Reload(Models, () => Models.Load(ModelsFile)),
            ["items"] = () => Reload(ClientItemDefinitions, () => LoadItems()),
            ["collections"] = () => Reload(Collections, () => Collections.Load(CollectionsFile)),
            ["nodepools"] = () => Reload(CollectionNodePools, () => CollectionNodePools.Load(CollectionNodePoolsFile)),
            ["nodetypes"] = () => Reload(CollectionNodeTypes, () => CollectionNodeTypes.Load(CollectionNodeTypesFile)),
            ["nodespawns"] = () => Reload(CollectionNodeSpawns, () => CollectionNodeSpawns.Load(CollectionNodeSpawnsDirectory)),
            ["coinstore"] = () => Reload(CoinStoreItems, () => CoinStoreItems.Load(CoinStoreItemsFile))
                && Reload(CoinStoreBundles, () => CoinStoreBundles.Load(CoinStoreBundlesFile)),
            ["itemclasses"] = () => Reload(ItemClasses, () => ItemClasses.Load(ItemClassesFile)),
            ["itemcategories"] = () => Reload(ItemCategories, () => ItemCategories.Load(ItemCategoriesFile)),
            ["itemcategorygroups"] = () => Reload(ItemCategoryGroups, () => ItemCategoryGroups.Load(ItemCategoryGroupsFile)),
            ["houses"] = () => Reload(Houses, () => Houses.Load(HousesFile)),
            ["housingfixtures"] = () => Reload(HousingFixtures, () => HousingFixtures.Load(HousingFixturesFile)),
            ["fishing"] = () => Fishing.Load(FishingFile),
            ["challenges"] = () => Reload(Challenges, () => Challenges.Load(ChallengesFile)),
            ["battles"] = () => Reload(Battles, () => Battles.Load(BattlesFile)),
            ["achievements"] = () => Reload(Achievements, () => Achievements.Load(AchievementsFile)),
            ["recipes"] = () => Reload(Recipes, () => LoadRecipes()),
            ["stations"] = () => Reload(Stations, () => Stations.Load(StationsFile)),
            ["gifts"] = () => Reload(Gifts, () => LoadGifts()),
            ["mounts"] = () => Reload(Mounts, () => Mounts.Load(MountsFile)),
            ["minigames"] = () => Reload(Minigames, () => Minigames.Load(MinigamesDirectory)),
            ["profiles"] = () => Reload(Profiles, () => Profiles.Load(ProfilesFile)),
            ["quickchats"] = () => Reload(QuickChats, () => QuickChats.Load(QuickChatsFile)),
            ["titles"] = () => Reload(PlayerTitles, () => PlayerTitles.Load(PlayerTitlesFile)),
            ["pois"] = () => Reload(PointOfInterests, () => PointOfInterests.Load(PointOfInterestsFile)),
            ["seasons"] = () => Reload(Seasons, () => Seasons.Load(SeasonsFile)),
            ["npcs"] = () => Reload(Npcs, () => Npcs.Load(NpcsFile)),
            ["rewards"] = () => Reload(RewardTables, () => RewardTables.Load(RewardTablesFile)),
            ["abilities"] = () => Reload(CombatAbilities, () => CombatAbilities.Load(CombatAbilitiesFile)),
            ["jobs"] = () => Reload(CombatJobs, () => CombatJobs.Load(CombatJobsFile)),
            ["mobs"] = () => Reload(Mobs, () => Mobs.Load(MobsFile)),
            ["itemabilities"] = () => Reload(ItemAbilities, () => ItemAbilities.Load(ItemAbilitiesFile)),
            ["namefilter"] = () => NameFilter.Load(NameFilterFile),
            ["firsttimeevents"] = () => Reload(FirstTimeEvents, () => FirstTimeEvents.Load(FirstTimeEventsFile)),
            ["minigametutorials"] = () => Reload(MiniGameTutorials, () => MiniGameTutorials.Load(MiniGameTutorialsFile))
        };
    }

    public IEnumerable<string> ReloadableResources => _reloaders.Keys;

    public event Action<string>? Reloaded;

    public bool TryReload(string name)
    {
        if (!_reloaders.TryGetValue(name, out var reload))
            return false;

        _fileSystemWatcher.EnableRaisingEvents = false;

        bool reloaded;

        try
        {
            reloaded = reload();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error reloading resource {name}.", name);
            return false;
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }

        if (reloaded)
            Reloaded?.Invoke(name);

        return reloaded;
    }

    /// <summary>
    /// The item definitions, with every recipe ingredient stacking (see <see cref="RecipeDefinitionCollection.UnlockIngredientStacks"/>).
    /// </summary>
    private bool LoadItems()
    {
        if (!ClientItemDefinitions.Load(ClientItemDefinitionsFile))
            return false;

        Recipes.UnlockIngredientStacks(ClientItemDefinitions);
        Gifts.MarkOpenable(ClientItemDefinitions);
        return true;
    }

    /// <summary>
    /// The gifts, after the reward tables they roll on and the items they open.
    /// </summary>
    private bool LoadGifts()
    {
        if (!Gifts.Load(GiftsFile, RewardTables))
            return false;

        Gifts.MarkOpenable(ClientItemDefinitions);
        return true;
    }

    private bool LoadRecipes()
    {
        if (!Recipes.Load(RecipesFile))
            return false;

        var unlocked = Recipes.UnlockIngredientStacks(ClientItemDefinitions);

        if (unlocked > 0)
            _logger.LogInformation("{count} recipe ingredients capped at one each now stack.", unlocked);

        return true;
    }

    private static bool Reload<TKey, TValue>(ObservableConcurrentDictionary<TKey, TValue> collection, Func<bool> load) where TKey : notnull
    {
        var previous = collection.ToList();

        collection.Clear();

        if (load())
            return true;

        collection.Clear();

        foreach (var entry in previous)
            collection.TryAdd(entry.Key, entry.Value);

        return false;
    }

    public bool Load()
    {
        if (!NameFilter.Load(NameFilterFile))
            return false;

        if (!HairMappings.Load(HairMappingsFile))
            return false;

        if (!HeadMappings.Load(HeadMappingsFile))
            return false;

        if (!SkinToneMappings.Load(SkinToneMappingsFile))
            return false;

        if (!FacePaintMappings.Load(FacePaintMappingsFile))
            return false;

        if (!ModelCustomizationMappings.Load(ModelCustomizationMappingsFile))
            return false;

        if (!FirstTimeEvents.Load(FirstTimeEventsFile))
            return false;

        if (!MiniGameTutorials.Load(MiniGameTutorialsFile))
            return false;

        if (!Models.Load(ModelsFile))
            return false;

        if (!ClientItemDefinitions.Load(ClientItemDefinitionsFile))
            return false;

        if (!Collections.Load(CollectionsFile))
            return false;

        if (!CollectionCoins.Load(CollectionCoinsFile))
            return false;

        if (!CollectionNodeTypes.Load(CollectionNodeTypesFile))
            return false;

        if (!CollectionNodePools.Load(CollectionNodePoolsFile))
            return false;

        if (!CollectionNodeSpawns.Load(CollectionNodeSpawnsDirectory))
            return false;

        foreach (var collection in Collections.Values)
        {
            if (collection.Entries.Any(entry => entry.ItemDefinitionId > 0 &&
                !ClientItemDefinitions.ContainsKey(entry.ItemDefinitionId)))
            {
                _logger.LogError("Collection {id} references an unknown item definition.", collection.Id);
                return false;
            }
        }

        var collectionItemDefinitionIds = Collections.Values
            .SelectMany(collection => collection.Entries)
            .Where(entry => entry.ItemDefinitionId > 0)
            .Select(entry => entry.ItemDefinitionId)
            .ToHashSet();

        foreach (var type in CollectionNodeTypes.Values)
        {
            if (!Models.ContainsKey(type.ModelId))
            {
                _logger.LogError("Collection node type {type} references an unknown model.", type.Key);
                return false;
            }
        }

        foreach (var spawn in CollectionNodeSpawns.Values)
        {
            if (!CollectionNodePools.TryGetValue(spawn.Pool, out var pool) ||
                pool.ZoneDefinitionId != spawn.ZoneDefinitionId)
            {
                _logger.LogError("Collection node spawn {id} references an unknown pool or mismatched zone.", spawn.Id);
                return false;
            }
        }

        foreach (var pool in CollectionNodePools.Values)
        {
            if (!CollectionNodeTypes.ContainsKey(pool.NodeType) ||
                pool.DropTable.Any(drop => !ClientItemDefinitions.ContainsKey(drop.ItemDefinitionId) ||
                    !collectionItemDefinitionIds.Contains(drop.ItemDefinitionId)))
            {
                _logger.LogError("Collection node pool {pool} has an invalid node type or collection drop reference.",
                    pool.Key);
                return false;
            }
        }

        if (!RewardTables.Load(RewardTablesFile))
            return false;

        if (!CombatAbilities.Load(CombatAbilitiesFile))
            return false;

        if (!CombatJobs.Load(CombatJobsFile))
            return false;

        if (!Mobs.Load(MobsFile))
            return false;

        foreach (var kit in CombatJobs.Values)
        {
            if (kit.FallbackBasicAbilityId != 0 && !CombatAbilities.ContainsKey(kit.FallbackBasicAbilityId) ||
                kit.Weapons.Any(w => !CombatAbilities.ContainsKey(w.BasicAbilityId) ||
                                     w.SpecialAbilityId != 0 && !CombatAbilities.ContainsKey(w.SpecialAbilityId)))
            {
                _logger.LogError("Combat job kit {id} references an unknown ability definition.", kit.ProfileId);
                return false;
            }
        }

        foreach (var table in RewardTables.Values)
        {
            // NOTE: We may need to fiddle with this if we ever extend past just items and currencies,
            // though isn't everything technically an item? Didn't think that far ahead.
            if (table.DropTable.OfType<ItemRewardDropDefinition>().Any(drop => !ClientItemDefinitions.ContainsKey(drop.ItemDefinitionId)))
            {
                _logger.LogError("Reward table {key} references an unknown item definition.", table.Key);
                return false;
            }
        }

        if (!CoinStoreItems.Load(CoinStoreItemsFile))
            return false;

        if (!CoinStoreBundles.Load(CoinStoreBundlesFile))
            return false;

        if (!ItemClasses.Load(ItemClassesFile))
            return false;

        if (!EquipmentSlots.Load(EquipmentSlotDefinitionsFile))
            return false;

        if (!ItemCategories.Load(ItemCategoriesFile))
            return false;

        if (!ItemCategoryGroups.Load(ItemCategoryGroupsFile))
            return false;

        if (!ZoneEnvironments.Load(ZoneEnvironmentsDirectory))
            return false;

        if (!Zones.Load(ZonesDirectory))
            return false;

        foreach (var pool in CollectionNodePools.Values)
        {
            if (!Zones.ContainsKey(pool.ZoneDefinitionId))
            {
                _logger.LogError("Collection node pool {pool} references unknown zone {zone}.",
                    pool.Key, pool.ZoneDefinitionId);
                return false;
            }
        }

        if (!Houses.Load(HousesFile))
            return false;

        if (!HousingFixtures.Load(HousingFixturesFile))
            return false;

        if (!Fishing.Load(FishingFile))
            return false;

        if (!Challenges.Load(ChallengesFile))
            return false;

        if (!Battles.Load(BattlesFile))
            return false;

        if (!Achievements.Load(AchievementsFile))
            return false;

        if (!LoadRecipes())
            return false;

        if (!Stations.Load(StationsFile))
            return false;

        if (!LoadGifts())
            return false;

        if (!Pets.Load(PetsFile))
            return false;

        if (!Minigames.Load(MinigamesDirectory))
            return false;

        if (!Mounts.Load(MountsFile))
            return false;

        if (!Profiles.Load(ProfilesFile))
            return false;

        if (!QuickChats.Load(QuickChatsFile))
            return false;

        if (!PlayerTitles.Load(PlayerTitlesFile))
            return false;

        if (!PointOfInterests.Load(PointOfInterestsFile))
            return false;

        if (!Seasons.Load(SeasonsFile))
            return false;

        if (!Consumables.Load(ConsumablesFile))
            return false;

        if (!ItemAbilities.Load(ItemAbilitiesFile))
            return false;

        if (!Npcs.Load(NpcsFile))
            return false;

        if (!NpcMovements.Load(NpcMovementsFile))
            return false;

        if (!NpcAmbient.Load(NpcAmbientFile))
            return false;

        if (!Quests.Load(QuestsFile))
            return false;

        foreach (var coin in CollectionCoins.Values)
        {
            Collections.TryGetValue(coin.CollectionId, out var collection);
            var entry = collection?.Entries.SingleOrDefault(entry => entry.Id == coin.EntryId);

            if (!Npcs.ContainsKey(coin.NpcDefinitionId) ||
                entry is null || entry.ItemDefinitionId > 0)
            {
                _logger.LogError(
                    "Collection coin NPC {npc} references an unknown NPC or a non-direct collection entry.",
                    coin.NpcDefinitionId);
                return false;
            }

        }

        return true;
    }

    public bool LoadZoneGeometry()
    {
        if (!Maps.Load(MapsDirectory))
            return false;

        if (!Terrains.Load(TerrainDirectory))
            return false;

        if (!NavMeshes.Load(NavMeshDirectory))
            return false;

        NavMeshes.BuildMissing(Terrains, NavMeshDirectory);

        return true;
    }

    /// <summary>
    /// Waits, up to a second, until a changed file can be read: the watcher hears of a change while it's being written
    /// (a copy into the bin folder, say), and reading it then fails with "being used by another process".
    /// </summary>
    private static bool WaitUntilWritten(string path)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                // Refused while anyone still has it open for writing.
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                return true;
            }
            catch (IOException)
            {
                Thread.Sleep(50);
            }
        }

        return false;
    }

    private void _fileSystemWatcher_Changed(object sender, FileSystemEventArgs e)
    {
        if (e.FullPath.EndsWith(".tmp", StringComparison.OrdinalIgnoreCase) || !File.Exists(e.FullPath))
            return;

        var directoryPath = Path.GetDirectoryName(e.FullPath);
        var isTopLevelResource = string.Equals(directoryPath, BaseDirectory, StringComparison.OrdinalIgnoreCase);
        var collectionNodeSpawnPrefix = CollectionNodeSpawnsDirectory + Path.DirectorySeparatorChar;
        var isCollectionNodeSpawn = directoryPath is not null &&
            directoryPath.StartsWith(collectionNodeSpawnPrefix, StringComparison.OrdinalIgnoreCase);

        var isMinigame = string.Equals(directoryPath, MinigamesDirectory, StringComparison.OrdinalIgnoreCase);

        if (!isTopLevelResource && !isCollectionNodeSpawn && !isMinigame)
            return;

        try
        {
            if (File.GetAttributes(e.FullPath).HasFlag(FileAttributes.Directory))
                return;

            _fileSystemWatcher.EnableRaisingEvents = false;

            if (!WaitUntilWritten(e.FullPath))
            {
                _logger.LogWarning("A changed file was still being written a second later, so it wasn't reloaded. File: {filepath}", e.FullPath);
                return;
            }

            var loaded = false;

            if (e.FullPath == HairMappingsFile)
                loaded = HairMappings.Load(HairMappingsFile);
            else if (e.FullPath == HeadMappingsFile)
                loaded = HeadMappings.Load(HeadMappingsFile);
            else if (e.FullPath == SkinToneMappingsFile)
                loaded = SkinToneMappings.Load(SkinToneMappingsFile);
            else if (e.FullPath == FacePaintMappingsFile)
                loaded = FacePaintMappings.Load(FacePaintMappingsFile);
            else if (e.FullPath == ModelCustomizationMappingsFile)
                loaded = ModelCustomizationMappings.Load(ModelCustomizationMappingsFile);
            else if (e.FullPath == ModelsFile)
                loaded = Models.Load(ModelsFile);
            else if (e.FullPath == FirstTimeEventsFile)
                loaded = FirstTimeEvents.Load(FirstTimeEventsFile);
            else if (e.FullPath == MiniGameTutorialsFile)
                loaded = MiniGameTutorials.Load(MiniGameTutorialsFile);
            else if (e.FullPath == ClientItemDefinitionsFile)
                loaded = LoadItems();
            else if (e.FullPath == CollectionsFile)
                loaded = Collections.Load(CollectionsFile);
            else if (e.FullPath == CollectionCoinsFile)
                loaded = CollectionCoins.Load(CollectionCoinsFile);
            else if (e.FullPath == CollectionNodePoolsFile)
                loaded = CollectionNodePools.Load(CollectionNodePoolsFile);
            else if (e.FullPath == CollectionNodeTypesFile)
                loaded = CollectionNodeTypes.Load(CollectionNodeTypesFile);
            else if (isCollectionNodeSpawn &&
                string.Equals(Path.GetExtension(e.FullPath), ".json", StringComparison.OrdinalIgnoreCase))
                loaded = CollectionNodeSpawns.Load(CollectionNodeSpawnsDirectory);
            else if (e.FullPath == ItemClassesFile)
                loaded = ItemClasses.Load(ItemClassesFile);
            else if (e.FullPath == EquipmentSlotDefinitionsFile)
                loaded = EquipmentSlots.Load(EquipmentSlotDefinitionsFile);
            else if (e.FullPath == ItemCategoriesFile)
                loaded = ItemCategories.Load(ItemCategoriesFile);
            else if (e.FullPath == ItemCategoryGroupsFile)
                loaded = ItemCategoryGroups.Load(ItemCategoryGroupsFile);
            else if (e.FullPath == HousesFile)
                loaded = Houses.Load(HousesFile);
            else if (e.FullPath == HousingFixturesFile)
                loaded = Reload(HousingFixtures, () => HousingFixtures.Load(HousingFixturesFile));
            else if (e.FullPath == FishingFile)
                loaded = Fishing.Load(FishingFile);
            else if (e.FullPath == ChallengesFile)
                loaded = Reload(Challenges, () => Challenges.Load(ChallengesFile));
            else if (e.FullPath == BattlesFile)
                loaded = Reload(Battles, () => Battles.Load(BattlesFile));
            else if (e.FullPath == AchievementsFile)
                loaded = Reload(Achievements, () => Achievements.Load(AchievementsFile));
            else if (e.FullPath == RecipesFile)
                loaded = Reload(Recipes, () => LoadRecipes());
            else if (e.FullPath == StationsFile)
                loaded = Reload(Stations, () => Stations.Load(StationsFile));
            else if (e.FullPath == GiftsFile)
                loaded = Reload(Gifts, () => LoadGifts());
            else if (e.FullPath == PetsFile)
                loaded = Pets.Load(PetsFile);
            else if (isMinigame &&
                string.Equals(Path.GetExtension(e.FullPath), ".json", StringComparison.OrdinalIgnoreCase))
                loaded = Reload(Minigames, () => Minigames.Load(MinigamesDirectory));
            else if (e.FullPath == MountsFile)
                loaded = Mounts.Load(MountsFile);
            else if (e.FullPath == ProfilesFile)
                loaded = Profiles.Load(ProfilesFile);
            else if (e.FullPath == QuickChatsFile)
                loaded = QuickChats.Load(QuickChatsFile);
            else if (e.FullPath == PlayerTitlesFile)
                loaded = PlayerTitles.Load(PlayerTitlesFile);
            else if (e.FullPath == PointOfInterestsFile)
                loaded = PointOfInterests.Load(PointOfInterestsFile);
            else if (e.FullPath == SeasonsFile)
                loaded = Reload(Seasons, () => Seasons.Load(SeasonsFile));
            else if (e.FullPath == NpcsFile)
                loaded = Npcs.Load(NpcsFile);
            else if (e.FullPath == NpcMovementsFile)
                loaded = NpcMovements.Load(NpcMovementsFile);
            else if (e.FullPath == NpcAmbientFile)
                loaded = NpcAmbient.Load(NpcAmbientFile);
            else if (e.FullPath == NameFilterFile)
                loaded = NameFilter.Load(NameFilterFile);
            else if (e.FullPath == RewardTablesFile)
                loaded = RewardTables.Load(RewardTablesFile);
            else if (e.FullPath == CombatAbilitiesFile)
                loaded = CombatAbilities.Load(CombatAbilitiesFile);
            else if (e.FullPath == CombatJobsFile)
                loaded = CombatJobs.Load(CombatJobsFile);
            else if (e.FullPath == MobsFile)
                loaded = Mobs.Load(MobsFile);
            else if (e.FullPath == ItemAbilitiesFile)
                loaded = Reload(ItemAbilities, () => ItemAbilities.Load(ItemAbilitiesFile));
            else if (e.FullPath == QuestsFile)
                loaded = Quests.Load(QuestsFile);
            else
                _logger.LogWarning("Unknown file changed. File: {filepath}", e.FullPath);

            if (!loaded)
                _logger.LogError("Error loading modified file. File: {filepath}", e.FullPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling modified file. File: {filepath}", e.FullPath);
        }
        finally
        {
            _fileSystemWatcher.EnableRaisingEvents = true;
        }
    }

    public void Dispose()
    {
        _fileSystemWatcher.Changed -= _fileSystemWatcher_Changed;
        _fileSystemWatcher.Dispose();
    }

}
