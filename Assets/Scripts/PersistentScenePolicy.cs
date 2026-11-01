using UnityEngine.SceneManagement;

/// <summary>Escenas en las que se conserva el personaje y la sesion de juego.</summary>
public static class PersistentScenePolicy
{
    public static bool IsAllowed(Scene scene)
    {
        return scene.IsValid() && (scene.name == "VillageMenuScene" ||
            scene.name == "GameScene" || scene.name == "MultiplayerGameScene");
    }
}
