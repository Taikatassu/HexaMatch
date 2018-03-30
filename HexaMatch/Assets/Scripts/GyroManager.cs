using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GyroManager : MonoBehaviour
{
    public RectTransform gyro_indicator_arrow_x;
    public RectTransform gyro_indicator_arrow_y;
    public RectTransform gyro_indicator_arrow_z;
    public Button toggle_gyro_button;
    public GameObject gyro_indicator_holder;

    private Gyroscope gyro;
    private bool gyro_enabled = false;

    private void Start()
    {
        toggle_gyro_button.onClick.AddListener(OnToggleGyroButtonPressed);
        ToggleGyro(false);
    }

    private bool ToggleGyro(bool state)
    {
        if (state)
        {
            if (SystemInfo.supportsGyroscope)
            {
                gyro_indicator_holder.SetActive(true);

                gyro = Input.gyro;
                gyro.enabled = true;
                gyro_enabled = true;
                return true;
            }

            print("System does not support gyroscope.");
            gyro_enabled = false;
            return false;
        }
        else
        {
            gyro_indicator_holder.SetActive(false);

            if (SystemInfo.supportsGyroscope)
            {
                gyro = Input.gyro;
                gyro.enabled = false;
                gyro_enabled = false;
                return true;
            }

            gyro_enabled = false;
            return false;
        }
    }

    private void Update()
    {
        if (gyro_enabled)
        {
            Vector3 gyro_attitude = gyro.attitude.eulerAngles;

            Vector3 gyro_arrow_x_new_rot = gyro_indicator_arrow_x.eulerAngles;
            gyro_arrow_x_new_rot.z = gyro_attitude.x;
            gyro_indicator_arrow_x.eulerAngles = gyro_arrow_x_new_rot;

            Vector3 gyro_arrow_y_new_rot = gyro_indicator_arrow_y.eulerAngles;
            gyro_arrow_y_new_rot.z = gyro_attitude.y;
            gyro_indicator_arrow_y.eulerAngles = gyro_arrow_y_new_rot;

            Vector3 gyro_arrow_z_new_rot = gyro_indicator_arrow_z.eulerAngles;
            gyro_arrow_z_new_rot.z = gyro_attitude.y;
            gyro_indicator_arrow_z.eulerAngles = gyro_arrow_z_new_rot;
        }
    }

    private void OnToggleGyroButtonPressed()
    {
        if (gyro_enabled)
        {
            print("Trying to disable gyro.");
            ToggleGyro(false);
        }
        else
        {
            print("Trying to enable gyro.");
            ToggleGyro(true);
        }
    }

    //protected void OnGUI()
    //{
    //    GUI.skin.label.fontSize = Screen.width / 40;

    //    GUILayout.Label("Gyroscope supported: " + gyro_supported);
    //    GUILayout.Label("Orientation: " + Screen.orientation);
    //    GUILayout.Label("input.gyro.attitude: " + Input.gyro.attitude);
    //    GUILayout.Label("phone width/font: " + Screen.width + " : " + GUI.skin.label.fontSize);
    //}

    ///********************************************/

    //// The Gyroscope is right-handed.  Unity is left handed.
    //// Make the necessary change to the camera.
    //void GyroModifyCamera()
    //{
    //    transform.rotation = GyroToUnity(Input.gyro.attitude);
    //}

    //private static Quaternion GyroToUnity(Quaternion q)
    //{
    //    return new Quaternion(q.x, q.y, -q.z, -q.w);
    //}

}
