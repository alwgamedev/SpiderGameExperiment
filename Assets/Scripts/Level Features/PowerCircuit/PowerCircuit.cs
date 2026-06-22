using UnityEngine;

public class PowerCircuit : MonoBehaviour
{
    //platform data (array of fixed joints)
    //int[] inletCount (how many inlets on each platform -- all inlets need to have power to activate the outlets)
    //int[] inletsWithPower
    //inlets (Transform, int platform)[]
    //outlets (PowerBeam, int platform)[]
    //+settings
    //+arrays of connections int[] connectedOutlet, int[] connectedInlet (-1 if no connection)
    //every update will:
    //1) removed invalid ouletConnections  -- if outlet and inlet no longer line up, delete the connection 
        //(decrementing inletsWithPower on the inlet platform)
    //2) update outlet activation -- turn off outlets on platforms with inletsWithPower[i] < inletCount[i]
    //3) check for new connections (incrementing inletsWithPower on the inlet platform, so inletsWithPower is accurate for next update).
        //the new connections may activate some more outlets, but their beams need time to grow, so won't create any
        //new connections immediately
}