using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Buttons : MonoBehaviour
{
	public Camera _camera;
	public GameObject acacia;
	public GameObject beech;
	public GameObject birch;
	public GameObject dryTree;
	public GameObject fir;
	public GameObject juniper;
	public GameObject maple;
	public GameObject oak;
	public GameObject pine;
	public GameObject spruce;
	public GameObject sycamore;
	
	public void Acacia()
	{
		
		_camera.transform.position = new Vector3(58.7f, 6f, -22.2f);
		acacia.transform.rotation = Quaternion.identity;
		acacia.SetActive(true);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
		public void Beech()
	{
		_camera.transform.position = new Vector3(92.18f, 9.36f, 11.6f);
		beech.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(true);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Birch()
	{
		_camera.transform.position = new Vector3(99.6f, 10.32f, -5f);
		birch.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(true);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void DryTree()
	{
		_camera.transform.position = new Vector3(90.75f, 5.5f, 13.84f);
		dryTree.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(true);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
		
		public void Fir()
	{
		_camera.transform.position = new Vector3(93.1f, 5.1f, 12.9f);
		fir.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(true);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Juniper()
	{
		_camera.transform.position = new Vector3(93.1f, 9.2f, 9.73f);
		juniper.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(true);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Maple()
	{
		_camera.transform.position = new Vector3(89.84f, 9.2f, 9.1f);
		maple.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(true);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Oak()
	{
		_camera.transform.position = new Vector3(92.53f, 9.2f, 9.1f);
		oak.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(true);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Pine()
	{
		_camera.transform.position = new Vector3(92.27f, 8.3f, 10.67f);
		pine.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(true);
		spruce.SetActive(false);
		sycamore.SetActive(false);
	}
	
		public void Spruce()
	{
		_camera.transform.position = new Vector3(92.27f, 12.0f, 14.0f);
		spruce.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(true);
		sycamore.SetActive(false);
	}
	
		public void Sycamore()
	{
		_camera.transform.position = new Vector3(91.4f, 12.0f, 13.29f);
		sycamore.transform.rotation = Quaternion.identity;
		acacia.SetActive(false);
		beech.SetActive(false);
		birch.SetActive(false);
		dryTree.SetActive(false);
		fir.SetActive(false);
		juniper.SetActive(false);
		maple.SetActive(false);
		oak.SetActive(false);
		pine.SetActive(false);
		spruce.SetActive(false);
		sycamore.SetActive(true);
	}
	
}
