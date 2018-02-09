﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Task : MonoBehaviour {

	public event System.Action OnLose;
	public event System.Action OnWin;
    public float restartDelay = .1f;

    protected virtual void TaskCompleted()
    {
        if (OnWin != null)
        {
            OnWin();
        }
    }

	protected virtual void TaskFailed()
	{
		if (OnLose != null)
		{
			OnLose();
		}
	}
}