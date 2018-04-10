using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SetTargetFramerate : MonoBehaviour {

    public int target_framerate = 60;

    private void Start()
    {
        Application.targetFrameRate = target_framerate;
    }
}
