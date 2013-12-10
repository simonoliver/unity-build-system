﻿using UnityEngine;
using System.Collections;
using System;

namespace UBS
{

	public class BuildStep : ScriptableObject {

		public string mTypeName = "";

		public System.Type mType;

		public void InferType()
		{
			mType = System.Type.GetType( mTypeName );
		}

		public void SetType(System.Type pType)
		{

			mType = pType;
			if(pType == null)
				mTypeName = "";
			else
				mTypeName = mType.ToString();
		}

	}
}