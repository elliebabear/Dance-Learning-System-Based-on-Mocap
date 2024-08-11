using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class walk : MonoBehaviour
{
    private Animator animator;
    private Camera mainCamera;
    // Start is called before the first frame update
    void Start()
    {
        animator = GetComponent<Animator>();
        mainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            SceneManager.LoadScene(0);
        }

        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");
        Vector3 dir = new Vector3(h, 0, v);
        if(dir!=Vector3.zero)
        {
            float p = mainCamera.transform.rotation.eulerAngles.y;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.Euler(0, p, 0), 100);
            //transform.rotation = Quaternion.LookRotation(dir);
            animator.SetBool("wasd", true);
            //transform.Translate(Vector3.forward * 1 * Time.deltaTime);
        }
        else
        {
            animator.SetBool("wasd", false);
        }
    }
}
