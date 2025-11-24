using UnityEngine;

/// <summary>
/// Configuration component that initializes the static GameManager.
/// Attach to a GameObject in the scene to configure game settings.
/// </summary>
public class GameManagerConfig : MonoBehaviour
{
    [Header("Scoring")]
    [SerializeField] private int startingBullets = 10;
    [SerializeField] private int killsForBonus = 2;
    [SerializeField] private int bonusBullets = 1;
    [SerializeField] private bool endGameWhenOutOfBullets = true;
    
    [Header("Escape System")]
    [SerializeField] private int maxEscapedBirds = 10;
    [SerializeField] private bool endGameOnEscapeLimit = true;

    void Awake()
    {
        GameManager.Initialize(
            bullets: startingBullets,
            maxEscapes: maxEscapedBirds,
            killBonus: killsForBonus,
            bonusAmmo: bonusBullets,
            endOnBullets: endGameWhenOutOfBullets,
            endOnEscapes: endGameOnEscapeLimit
        );
    }
}
