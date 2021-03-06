﻿using UnityEngine;
using System.Collections;
using System.IO.Ports;
using System;

public class ArmControlScript : MonoBehaviour {

    SerialPort stream;
    int newCount = 0;

    float yaw, pitch, roll;
    float dYaw, dPitch, dRoll;
    float x, y, z; // accelerations
    double time;
    string[] lastRead;
	// Use this for initialization
	void Start () {
        x = y = z = 0f;
        yaw = pitch = roll = 0f;
        dYaw = dPitch = dRoll = 0f;
        time = 0;
        stream = new SerialPort(SerialPort.GetPortNames()[0], 9600, Parity.None, 8, StopBits.One);
        stream.ReadTimeout = 50;
        stream.Handshake = Handshake.None;
        stream.DtrEnable = true;
        stream.RtsEnable = true;
        stream.Open();
    }
    float maxRoll = 0f;
    void process(string str)
    {
        string[] data = str.Split(' '); // split by space
        if (data.Length != 10) return;
        if (lastRead == null)
        { // calibration
            lastRead = data;
        }
        newCount++;
        // setup values of angles
        double dt = (Convert.ToInt32(data[9]) - Convert.ToInt32(lastRead[9])) * (0.000039);
        //Debug.Log(transform.rotation);
        time += dt;
        dRoll = (float)(Convert.ToSingle(data[3]) * dt) % 360;
        dPitch = (float)(Convert.ToSingle(data[4]) * dt) % 360;
        dYaw = (float)(Convert.ToSingle(data[5]) * dt) % 360;
        roll += dRoll;
        pitch += dPitch;
        yaw += dYaw;

        // get x, y, z values
        
        double ax = Convert.ToDouble(data[6]);
        double ay = Convert.ToDouble(data[7]);
        double az = Convert.ToDouble(data[8]);
        
        x = Convert.ToSingle(data[6]);
        y = Convert.ToSingle(data[7]);
        z = Convert.ToSingle(data[8]);
        
        if (Math.Sqrt(ax*ax+ay*ay+az*az)-1 < 0.3)
        {
            float pitchAcc = (float) (Math.Atan2(ax, az) * 180 / Math.PI);
            pitchAcc = (pitchAcc >= 0 ? pitchAcc : pitchAcc+360);
            pitch = pitch * 0.998f - pitchAcc * 0.002f;

            // Turning around the Y axis results in a vector on the X-axis
            float rollAcc = (float) (Math.Atan2(ay, az) * 180 / Math.PI);
            rollAcc = (rollAcc >= 0 ? rollAcc : rollAcc + 360);
            roll = roll * 0.998f - rollAcc * 0.002f;
        }

        pitch %= 360;
        yaw %= 360;
        roll %= 360;
        Debug.Log("roll: " + roll + ", pitch: " + pitch + ", yaw: " + yaw);
        transform.eulerAngles = new Vector3(pitch, yaw, roll)+ transform.parent.gameObject.transform.eulerAngles;// new Vector3(pitch, yaw, roll);
        //transform.Rotate(-dPitch, -dYaw, dRoll, Space.World);
        lastRead = data;

        GetComponent<Rigidbody>().AddForce(x, y, z);
    }

    // Update is called once per frame
    // gyroscope updates at 64000
    // accelerometer is 36000
    void Update () {
        //process("0 0 0 0.487805 0.000000 -0.060976 0.018555 0.340759 0.984070 209424");
        StartCoroutine
        (
            AsynchronousRead
            ((string s) => process(s),     // Callback
                () => DoNothing(), // Error callback
                10f                             // Timeout (seconds)
            )
        );
    }

    void DoNothing()
    {
        return;
    }

    void OnDestroy()
    {
        stream.Close();
    }
    public IEnumerator AsynchronousRead(Action<string> callback, Action fail = null, float timeout = float.PositiveInfinity)
    {
        DateTime initialTime = DateTime.Now;
        DateTime nowTime;
        TimeSpan diff = default(TimeSpan);

        string dataString = null;

        do
        {
            try
            {
                //Debug.Log(stream);
                //dataString = stream.ReadLine();
                if (!stream.IsOpen)
                {
                    Debug.Log("didn't open");
                    stream.Open();
                }
                dataString = stream.ReadTo("\r\n");
                //Debug.Log(dataString);
            }
            catch (TimeoutException)
            {
                dataString = null;
            }

            if (dataString != null)
            {
                callback(dataString);
                yield return null;
            }
            else
                yield return new WaitForSeconds(0.05f);

            nowTime = DateTime.Now;
            diff = nowTime - initialTime;

        } while (diff.Milliseconds < timeout);

        if (fail != null)
            fail();
        yield return null;
    }
}
