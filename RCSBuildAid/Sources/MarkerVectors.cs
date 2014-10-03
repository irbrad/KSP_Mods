/* Copyright © 2013-2014, Elián Hanisch <lambdae2@gmail.com>
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using UnityEngine;

namespace RCSBuildAid
{
    /* Component for calculate and show forces in CoM */
    public class MarkerVectors : MonoBehaviour
    {
        VectorGraphic transVector;
        VectorGraphic torqueVector;
        CircularVectorGraphic torqueCircle;
        float threshold = 0.05f;
        Vector3 torque = Vector3.zero;
        Vector3 translation = Vector3.zero;

        public GameObject Marker;
        public MomentOfInertia MoI;

        public Vector3 Thrust {
            get { return transVector == null ? Vector3.zero : transVector.value * -1; }
        }

        public Vector3 Torque {
            get { return torqueVector == null ? Vector3.zero : torqueVector.value; }
        }

        public new bool enabled {
            get { return base.enabled; }
            set { 
                base.enabled = value;
                if (torqueCircle == null) {
                    return;
                }
                transVector.gameObject.SetActive (value);
                torqueCircle.gameObject.SetActive (value);
                torqueVector.gameObject.SetActive (value);
            }
        }

        void Awake ()
        {
            /* layer change must be done before adding the Graphic components */
            GameObject obj = new GameObject ("Translation Vector Object");
            obj.layer = gameObject.layer;
            obj.transform.parent = transform;
            obj.transform.localPosition = Vector3.zero;
            transVector = obj.AddComponent<VectorGraphic> ();
            Color color = Color.green;
            color.a = 0.4f;
            transVector.color = color;
            transVector.offset = 0.6f;
            transVector.maxLength = 3f;
            transVector.minLength = 0.25f;
            transVector.maxWidth = 0.16f;
            transVector.minWidth = 0.05f;
            transVector.upperMagnitude = 5;
            transVector.lowerMagnitude = threshold;
            transVector.exponentialScale = true;

            obj = new GameObject ("Torque Vector Object");
            obj.layer = gameObject.layer;
            obj.transform.parent = transform;
            obj.transform.localPosition = Vector3.zero;
            torqueVector = obj.AddComponent<VectorGraphic> ();
            color = XKCDColors.ReddishOrange;
            color.a = 0.6f;
            torqueVector.color = color;
            torqueVector.offset = 0.6f;
            torqueVector.maxLength = 3f;
            torqueVector.minLength = 0.25f;
            torqueVector.maxWidth = 0.16f;
            torqueVector.minWidth = 0.05f;
            torqueVector.upperMagnitude = 5;
            torqueVector.lowerMagnitude = threshold;
            torqueVector.exponentialScale = true;

            obj = new GameObject ("Torque Circle Object");
            obj.layer = gameObject.layer;
            obj.transform.parent = transform;
            obj.transform.localPosition = Vector3.zero;
            torqueCircle = obj.AddComponent<CircularVectorGraphic> ();

            MoI = gameObject.AddComponent<MomentOfInertia> ();
        }

        void Start ()
        {
            if (RCSBuildAid.Reference == Marker) {
                /* we should start enabled */
                enabled = true;
            } else {
                enabled = false;
            }
        }

        Vector3 calcTorque (Transform transform, Vector3 force)
        {
            Vector3 lever = this.transform.position - transform.position;
            return Vector3.Cross (lever, force);
        }

        void sumForces (List<PartModule> moduleList)
        {
            foreach (PartModule mod in moduleList) {
                if (mod == null) {
                    continue;
                }
                ModuleForces mf = mod.GetComponent<ModuleForces> ();
                if (mf == null || !mf.enabled) {
                    continue;
                }
                for (int t = 0; t < mf.vectors.Length; t++) {
                    Vector3 force = -1 * mf.vectors [t].value; /* vectors represent exhaust force, 
                                                                  so -1 for actual thrust */
                    translation += force;
                    torque += calcTorque (mf.vectors [t].transform, force);
                }
            }
        }

        void LateUpdate ()
        {
            if (Marker == null) {
                return;
            }
            bool enabled, visible;
            if (RCSBuildAid.Enabled == false) {
                enabled = false;
            } else if (RCSBuildAid.mode == DisplayMode.none) {
                enabled = false;
            } else {
                enabled = Marker.activeInHierarchy;
            }
            visible = enabled && Marker.renderer.enabled;

            /* show vectors if visible */
            if (transVector.enabled != visible) {
                transVector.enabled = visible;
                torqueVector.enabled = visible;
                torqueCircle.enabled = visible;
            }
            if (!enabled) {
                transVector.value = Vector3.zero;
                torqueVector.value = Vector3.zero;
                torqueCircle.value = Vector3.zero;
                return;
            }
            transform.position = Marker.transform.position;
            /* calculate torque, translation and display them */
            torque = Vector3.zero;
            translation = Vector3.zero;

            switch(RCSBuildAid.mode) {
            case DisplayMode.RCS:
                sumForces (RCSBuildAid.RCSlist);
                if (RCSBuildAid.rcsMode == RCSMode.ROTATION) {
                    /* rotation mode, we want to reduce translation */
                    torqueVector.valueTarget = RCSBuildAid.Normal * -1;
                    transVector.valueTarget = Vector3.zero;
                } else {
                    /* translation mode, we want to reduce torque */
                    transVector.valueTarget = RCSBuildAid.Normal * -1;
                    torqueVector.valueTarget = Vector3.zero;
                }
                break;
            case DisplayMode.Engine:
                sumForces (RCSBuildAid.EngineList);
                torqueVector.valueTarget = Vector3.zero;
                transVector.valueTarget = Vector3.zero;
                break;
            }

            /* update vectors in CoM */
            torqueVector.value = torque;
            transVector.value = translation;

            if (torque != Vector3.zero) {
                if (MoI.value == 0) {
                    /* this only happens with single part crafts, because all mass is concentrated
                     * in the CoM, so lets just use torque */
                    torqueCircle.value = torque;
                } else {
                    torqueCircle.value = torque / MoI.value;
                }
                torqueCircle.transform.rotation = Quaternion.LookRotation (torque, translation);
            } else {
                torqueCircle.value = Vector3.zero;
            }
        }
    }
}

