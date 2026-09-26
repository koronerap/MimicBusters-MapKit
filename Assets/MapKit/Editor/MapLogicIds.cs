using System.Collections.Generic;
using System.Linq;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Gives every map entity in a scene a number of its own, the name the network uses for it
    /// (MapEntity.entityId, plan §16.1). On every save, so an author never sees it happen: a new entity
    /// gets the next free number, and a duplicated one (Ctrl+D copies the number too) a fresh one. Numbers
    /// already given are never changed, because a map's wiring does not depend on them but a published
    /// map's players' snapshots do.
    /// </summary>
    [InitializeOnLoad]
    public static class MapLogicIds
    {
        static MapLogicIds()
        {
            EditorSceneManager.sceneSaving -= OnSaving;
            EditorSceneManager.sceneSaving += OnSaving;
        }

        static void OnSaving(Scene scene, string path) => Ensure(scene);

        /// <summary>Numbers the entities that have none or share one. Returns how many changed.</summary>
        public static int Ensure(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            List<MapEntity> entities = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapEntity>(true))
                .ToList();

            var used = new HashSet<int>();
            var needing = new List<MapEntity>();
            foreach (MapEntity entity in entities)
            {
                if (entity.EntityId > 0 && used.Add(entity.EntityId))
                    continue;
                needing.Add(entity);
            }

            int next = used.Count > 0 ? used.Max() + 1 : 1;
            foreach (MapEntity entity in needing)
            {
                Undo.RecordObject(entity, "Map logic numbers");
                entity.EditorSetEntityId(next++);
                EditorUtility.SetDirty(entity);
            }

            if (needing.Count > 0)
                EditorSceneManager.MarkSceneDirty(scene);
            return needing.Count;
        }
    }
}
