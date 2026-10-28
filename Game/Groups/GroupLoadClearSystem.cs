using Game;
using Unity.Entities;
using UnityEngine.Scripting;

namespace FlowerPainter.Game.Groups
{
    // Game.Serialization.ClearSystem clears entities with vanilla game components, but our
    // headless group records have none. Remove the previous city's records before deserialization
    // so reloading cannot leave duplicate IDs or select an outdated receipt.
    public sealed partial class GroupLoadClearSystem : GameSystemBase
    {
        private EntityQuery _records;
        [Preserve] protected override void OnCreate()
        {
            base.OnCreate();
            _records = GetEntityQuery(ComponentType.ReadOnly<FlowerPainterGroup>());
        }
        [Preserve] protected override void OnUpdate() => EntityManager.DestroyEntity(_records);
    }
}
