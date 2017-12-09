﻿/*Copyright(c) <2017> <Benoit Constantin ( France )>

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. 
*/

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using System;

namespace BC_Solution.UnetNetwork
{
    public class TransformRotationSynchronizer : MovementSynchronizer
    {
            class TransformRotationState : State
            {
                public Vector3 m_rotation;

                public TransformRotationState(Transform t, float relativeTime)
                {
                    this.m_relativeTime = relativeTime;
                    m_rotation = t.rotation.eulerAngles;
                }
            }

            [SerializeField]
            new Transform transform;


            [Space(10)]
            [SerializeField]
            SYNCHRONISATION_MODE rotationSynchronizationMode;

            [SerializeField]
            [Tooltip("Angle in degree for minimum update")]
            float rotationThreshold = 0.1f;

            private Quaternion lastRotation = Quaternion.identity;

            private Quaternion extrapolationAngularVelocity;

            private Quaternion rotationError = Quaternion.identity;



            public override void OnEndExtrapolation(State rhs)
            {
                this.transform.rotation = Quaternion.Slerp(this.transform.rotation, Quaternion.Euler(((TransformRotationState)statesBuffer[0]).m_rotation), Time.deltaTime / interpolationErrorTime);
            }

            public override void OnBeginExtrapolation(State extrapolationState, float timeSinceInterpolation)
            {
                extrapolationAngularVelocity = (Quaternion.Euler(((TransformRotationState)statesBuffer[0]).m_rotation) * Quaternion.Inverse(Quaternion.Euler(((TransformRotationState)statesBuffer[1]).m_rotation)));
                Vector3 axis;
                float angle;
                extrapolationAngularVelocity.ToAngleAxis(out angle, out axis);
                angle /= ((statesBuffer[0].m_relativeTime - statesBuffer[1].m_relativeTime));
                extrapolationAngularVelocity = Quaternion.AngleAxis(angle, axis);
            }

            public override void OnErrorCorrection()
            {
                this.transform.rotation = Quaternion.Inverse(rotationError) * this.transform.rotation;
                rotationError = Quaternion.Slerp(rotationError, Quaternion.identity, Time.deltaTime / interpolationErrorTime);
                this.transform.rotation = rotationError * this.transform.rotation;
            }

            public override void OnExtrapolation()
            {
                Vector3 axis;
                float angle;
                extrapolationAngularVelocity.ToAngleAxis(out angle, out axis);
                angle *= Time.deltaTime;
                this.transform.rotation = Quaternion.AngleAxis(angle, axis) * this.transform.rotation;
            }

            public override void OnInterpolation(State rhs, State lhs, int lhsIndex, float t)
            {
                //ROTATION     
                Vector3 val = this.transform.rotation.eulerAngles;
                GetVector3(rotationSynchronizationMode, ref val, Quaternion.Slerp(Quaternion.Euler(((TransformRotationState)lhs).m_rotation), Quaternion.Euler(((TransformRotationState)rhs).m_rotation), t).eulerAngles);


                if (interpolationErrorTime > 0) /// We need to compensate error on extrapolation
                {
                    if (extrapolatingState != null) //We were extrapolating, we need to calcul the error
                        rotationError = this.transform.rotation * Quaternion.Inverse(Quaternion.Euler(val));
                }
                else
                    rotationError = Quaternion.identity;

                this.transform.rotation = rotationError * Quaternion.Euler(val);
            }


            public override void ResetStatesBuffer()
            {
                base.ResetStatesBuffer();

                lastRotation = this.transform.rotation;
            }

            public override bool NeedToUpdate()
            {
                if ((!base.NeedToUpdate()) ||
                     Quaternion.Angle(this.transform.rotation, lastRotation) < rotationThreshold)
                    return false;

                return true;
            }


            public override void GetCurrentState(NetworkWriter networkWriter)
            {
                lastRotation = this.transform.rotation;

                SerializeVector3(rotationSynchronizationMode, this.transform.rotation.eulerAngles, networkWriter);
            }


            public override void ReceiveCurrentState(float relativeTime, NetworkReader networkReader)
            {
                TransformRotationState newState = new TransformRotationState(this.transform, relativeTime);
                UnserializeVector3(rotationSynchronizationMode, ref newState.m_rotation, networkReader);

                AddState(newState);
            }

            public override void ReceiveSync(NetworkReader networkReader)
            {
                Vector3 val = Vector3.zero;

                UnserializeVector3(rotationSynchronizationMode, ref val, networkReader);
                this.transform.rotation = Quaternion.Euler(val);

                ResetStatesBuffer();
            }
    }
}
