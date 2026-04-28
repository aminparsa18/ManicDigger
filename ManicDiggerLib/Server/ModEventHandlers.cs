using ManicDigger;

public class ModEventHandlers
{
    public List<ModDelegates.WorldGenerator> getchunk = new();
    public List<ModDelegates.BlockUse> onuse = new();
    public List<ModDelegates.BlockBuild> onbuild = new();
    public List<ModDelegates.BlockDelete> ondelete = new();
    public List<ModDelegates.BlockUseWithTool> onusewithtool = new();
    public List<ModDelegates.ChangedActiveMaterialSlot> changedactivematerialslot = new();
    public List<ModDelegates.BlockUpdate> blockticks = new();
    public List<ModDelegates.PopulateChunk> populatechunk = new();
    public List<ModDelegates.Command> oncommand = new();
    public List<ModDelegates.WeaponShot> onweaponshot = new();
    public List<ModDelegates.WeaponHit> onweaponhit = new();
    public List<ModDelegates.SpecialKey1> onspecialkey = new();
    public List<ModDelegates.PlayerJoin> onplayerjoin = new();
    public List<ModDelegates.PlayerLeave> onplayerleave = new();
    public List<ModDelegates.PlayerDisconnect> onplayerdisconnect = new();
    public List<ModDelegates.PlayerChat> onplayerchat = new();
    public List<ModDelegates.PlayerDeath> onplayerdeath = new();
    public List<ModDelegates.DialogClick> ondialogclick = new();
    public List<ModDelegates.DialogClick2> ondialogclick2 = new();
    public List<ModDelegates.LoadWorld> onloadworld = new();
    public List<ModDelegates.UpdateEntity> onupdateentity = new();
    public List<ModDelegates.UseEntity> onuseentity = new();
    public List<ModDelegates.HitEntity> onhitentity = new();
    public List<ModDelegates.Permission> onpermission = new();
    public List<ModDelegates.CheckBlockUse> checkonuse = new();
    public List<ModDelegates.CheckBlockBuild> checkonbuild = new();
    public List<ModDelegates.CheckBlockDelete> checkondelete = new();
}
