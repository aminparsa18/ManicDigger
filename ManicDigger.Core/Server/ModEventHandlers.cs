using ManicDigger;

public class ModEventHandlers
{
    public List<ModDelegates.WorldGenerator> Getchunk { get; set; } = [];
    public List<ModDelegates.BlockUse> OnUse { get; set; } = [];
    public List<ModDelegates.BlockBuild> OnBuild { get; set; } = [];
    public List<ModDelegates.BlockDelete> OnDelete { get; set; } = [];
    public List<ModDelegates.BlockUseWithTool> OnUseWithTool { get; set; } = [];
    public List<ModDelegates.ChangedActiveMaterialSlot> ChangedActiveMaterialSlot { get; set; } = [];
    public List<ModDelegates.BlockUpdate> BlockTicks { get; set; } = [];
    public List<ModDelegates.PopulateChunk> PopulateChunk { get; set; } = [];
    public List<ModDelegates.Command> OnCommand { get; set; } = [];
    public List<ModDelegates.WeaponShot> OnWeaponShot { get; set; } = [];
    public List<ModDelegates.WeaponHit> OnWeaponHit { get; set; } = [];
    public List<ModDelegates.SpecialKey1> OnspecialKey { get; set; } = [];
    public List<ModDelegates.PlayerJoin> OnPlayerJoin { get; set; } = [];
    public List<ModDelegates.PlayerLeave> OnPlayerLeave { get; set; } = [];
    public List<ModDelegates.PlayerDisconnect> OnPlayerDisconnect { get; set; } = [];
    public List<ModDelegates.PlayerChat> OnPlayerChat { get; set; } = [];
    public List<ModDelegates.PlayerDeath> OnPlayerDeath { get; set; } = [];
    public List<ModDelegates.DialogClick> OnDialogClick { get; set; } = [];
    public List<ModDelegates.DialogClick2> OnDialogClick2 { get; set; } = [];
    public List<ModDelegates.LoadWorld> OnLoadWorld { get; set; } = [];
    public List<ModDelegates.UpdateEntity> OnUpdateEntity { get; set; } = [];
    public List<ModDelegates.UseEntity> OnUseEntity { get; set; } = [];
    public List<ModDelegates.HitEntity> OnHitEntity { get; set; } = [];
    public List<ModDelegates.Permission> OnPermission { get; set; } = [];
    public List<ModDelegates.CheckBlockUse> CheckOnUse { get; set; } = [];
    public List<ModDelegates.CheckBlockBuild> CheckOnBuild { get; set; } = [];
    public List<ModDelegates.CheckBlockDelete> CheckOnDelete { get; set; } = [];
}
