using System;
using System.Collections.Generic;

public static partial class ResourcePaths
{
    public const string GAME_OVER_UI_PATH = "UI/GameOverUI";
    public const string GAMEPLAY_CONTROLS_UI_PATH = "GameManagers/GameplayControlsUI";

    private static readonly Dictionary<Type, string> _uiPaths = new Dictionary<Type, string>
    {
        { typeof(GameOverUI), GAME_OVER_UI_PATH },
        { typeof(GameplayControlsUI), GAMEPLAY_CONTROLS_UI_PATH }
    };

    /// <summary>uiType에 등록된 리소스 경로를 path에 저장하고 등록 여부를 반환한다.</summary>
    public static bool TryGetUIPath(Type uiType, out string path)
    {
        return _uiPaths.TryGetValue(uiType, out path);
    }
}
