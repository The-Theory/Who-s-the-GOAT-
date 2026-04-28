using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Unity.Collections.AllocatorManager;

public class StartGame : MonoBehaviour
{
    public Button myButton;
    void Start()
    {
        myButton.onClick.AddListener(MyFunction); // Assigns function at runtime
    }
}
