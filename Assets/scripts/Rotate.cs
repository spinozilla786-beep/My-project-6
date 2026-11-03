using UnityEngine;

public class Rotate: MonoBehaviour
{
    

void Update()
{

transform.Rotate(new Vector3(15, 30, 45) * Time.deltaTime);

//this is how to make one singluar object rotate if needed

}

}