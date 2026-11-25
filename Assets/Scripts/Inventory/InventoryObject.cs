using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System;

public class InventoryObject : MonoBehaviour
{

    public string Name;
    public string Objectname;

    //En caso de tener dos objetos con el mismo nombres
    public int ID;

    //Cuanto espacio ocupa
    public int weight;

    public List<List<int>> requieredCells = new List<List<int>>();

    [Header("Feedback de selección")]
    [SerializeField] private Image itemImage;
    [SerializeField] private Color selectedColor = Color.gray;

    private Color originalColor;

    public event Action<bool> OnSelectionChanged;

    private bool selected;


    public bool isSelected
    {
        get => selected;
        set 
        {
            if (selected == value) return;

            selected = value;
            
            OnSelectionChanged?.Invoke(selected);
        }
    }

    // public bool isSelected = false;

    protected virtual void Awake()
    {
        if (itemImage == null) itemImage = GetComponent<Image>();
        if (itemImage != null) originalColor = itemImage.color;
    }
    
    public void SetDisplayImage(Image image)
    {
        itemImage = image;

        if (itemImage != null)
        {
            originalColor = itemImage.color;
        }
    }

    public void SetInventorySelectionVisual(bool selectedForMove)
    {
        if (itemImage == null) return;

        itemImage.color = selectedForMove ? selectedColor : originalColor;
    }

    public virtual Sprite GetUISprite()
    {
        return null;
    }

    protected void getRealList(List<int> cells)
    {
        for(int i = 0; i < cells.Count; i+=2)
        {
            requieredCells.Add(new List<int> {cells[i],cells[i+1]});
        }
    }
}
