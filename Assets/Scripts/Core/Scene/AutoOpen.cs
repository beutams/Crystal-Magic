using System.Collections;
using System.Collections.Generic;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEngine;

public class AutoOpen : MonoBehaviour
{
    private void Awake()
    {
        SubScene subScene = GetComponent<SubScene>();
        SubSceneUtility.EditScene(subScene);
    }
}
