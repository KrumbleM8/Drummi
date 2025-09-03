using UnityEngine;
using System.Collections.Generic;

public class Beat
{
    public float duration;
    public float timeSlot;
    public bool isBongoSide; // true: right bongo, false: left bongo

    public Beat(float duration, float timeSlot, bool isBongoSide)
    {
        this.duration = duration;
        this.timeSlot = timeSlot;
        this.isBongoSide = isBongoSide;
    }
}
