// ROL: kar izini kare kare bıraktıran geçici sınama yürütücüsü.
// Çağıran: F1 panelindeki "Otomatik yürü" düğmesi.

using UnityEngine;

/// İZ SINAMASI KARE KARE OLMAK ZORUNDA.
///
/// Yakalama pass'i kare başına bir kez koşuyor. Oyuncuyu tek karede birçok kez
/// hareket ettirmek yalnız SON konumu yakalatıyor ve ekranda tek bir damga
/// bırakıyor — "iz sürekli değil, iki ayrı damga" belirtisi bunun ürünüydü,
/// sistemin değil (ölçüldü).
///
/// Bu bileşen her karede bir adım attırıyor; iz gerçek yürüyüşteki gibi
/// birikiyor. İşi bitince kendini siliyor.
[DisallowMultipleComponent]
public class SnowWalkProbe : MonoBehaviour
{
    [Tooltip("Yürüme yönü ve kare başına alınan yol (m).")]
    public Vector3 AdimVektoru = new(0.045f, 0f, 0f);

    [Tooltip("Kaç kare yürüyecek.")]
    public int KalanKare = 120;

    CharacterController govde;

    void Awake() => govde = GetComponent<CharacterController>();

    void Update()
    {
        if (govde == null || KalanKare <= 0)
        {
            Destroy(this);
            return;
        }

        // Yerçekimi payı: zemine oturmuş kalsın.
        govde.Move(AdimVektoru + Vector3.down * 0.03f);
        KalanKare--;
    }
}
