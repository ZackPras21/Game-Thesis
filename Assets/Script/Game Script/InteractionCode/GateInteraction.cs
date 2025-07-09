using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GateInteraction : MonoBehaviour
{
    //public GameObject spriteButton;
    public GameObject Gate;
    public GameObject GateDua;
    public ParticleSystem particleSystemGate;
    public void OpenGate()
    {
        Instantiate(particleSystemGate, Gate.transform.position, Gate.transform.rotation, gameObject.transform);
        StartCoroutine(OpeningGate());
    }
    public void CloseGate()
    {
        StartCoroutine(ClosingGate());
        Instantiate(particleSystemGate, Gate.transform.position, Gate.transform.rotation, gameObject.transform);
    }
    private IEnumerator OpeningGate()
    {
        yield return new WaitForSeconds(3);
        Gate.SetActive(false);
        GateDua.SetActive(false);
    }
    private IEnumerator ClosingGate()
    {
        Gate.SetActive(true);
        GateDua.SetActive(true);
        yield return null;
    }
}
