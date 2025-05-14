using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Audio;

public class AudioManager : MonoBehaviour
{
    private PlayerState audioState = PlayerState.Idle;
    public static AudioManager instance { get; private set; }

    [Header("----------------Audio Sources----------------")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource SFXSource;
    [SerializeField] private AudioSource playerSFXSource;
    [SerializeField] private AudioSource playerParrySource;
    [SerializeField] private AudioSource playerSFXMovementSource;
    [SerializeField] private AudioSource playerSFXAttackSource;
    [SerializeField] private AudioSource playerSFXDashSource;
    [SerializeField] private AudioSource environmentSFXSource;
    [SerializeField] private AudioSource enemyAudioSource;

    [Header("----------------Mixer Settings----------------")]
    [SerializeField] private AudioMixer myMixer;


    [Header("----------------Audio Clips----------------")]
    public AudioClip theme;
    public AudioClip inGameTheme;
    public AudioClip bossTheme;
    public AudioClip chest;
    public AudioClip parry;
    public AudioClip parrySuccess;
    public AudioClip tower;
    public AudioClip gate;
    public AudioClip gateClose;

    [Header("----------------Player Man----------------")]
    public AudioClip deathMan;
    public AudioClip dashMan;
    public AudioClip[] playerManMovementSfx;
    public AudioClip[] playerManCombatSfx;
    public AudioClip[] playerManHurtSfx;

    [Header("----------------Player Woman----------------")]
    public AudioClip deathWoman;
    public AudioClip dashWoman;
    public AudioClip[] playerWomanMovementSfx;
    public AudioClip[] playerWomanCombatSfx;
    public AudioClip[] playerWomanHurtSfx;

    [Header("----------------Enemy Audio----------------")]
    [Header("Enemy Creep----------------")]

    // Creep Enemy Audio Clips
    public AudioClip creepAlertedClip;
    public AudioClip[] creepAttackClip;
    public AudioClip creepGetHitClip;
    public AudioClip creepDieClip;

    // Medium1 Enemy Audio Clips
    [Header("Enemy Medium 1----------------")]
    public AudioClip medium1AlertedClip;
    public AudioClip[] medium1AttackClip;
    public AudioClip medium1GetHitClip;
    public AudioClip medium1DieClip;

    // Medium2 Enemy Audio Clips
    [Header("Enemy Medium 2----------------")]
    public AudioClip medium2AlertedClip;
    public AudioClip[] medium2AttackClip;
    public AudioClip medium2GetHitClip;
    public AudioClip medium2DieClip;

    [Header("BOSS----------------")]
    public AudioClip bossNormalAttackClip;
    public AudioClip bossGroundAttackAClip;
    public AudioClip bossGroundAttackBClip;
    public AudioClip bossSpinAttackClip;
    public AudioClip bossUltimateAttackClip;
    public AudioClip bossDeathClip;
    public AudioClip[] bossHurtClip;
    public AudioClip[] bossMovementClip;

    [Header("----------------Box Destruction----------------")]
    public AudioClip boxCrack;
    public AudioClip[] boxGettingHit;

    [Header("----------------NPC----------------")]
    public AudioClip npcHovering;
    public AudioClip npcSalto;
    public AudioClip[] npcTalk;

    private PlayerController player;
    int i = 1;
    int j = 0;
    int creepCount = 0;
    int medium1Count = 0;
    int medium2Count = 0;
    public bool IsBossPlay = false;

    [Header("UI Sounds")]
    public AudioClip buttonClick;
    public AudioClip buttonHover;

    //CurrentLevel
    public string currentLevel = "";

    //............
    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == "NewLevelOne")
        {
            player = PlayerController.Instance;
            // Mulai ulang audio jika perlu, atau sesuaikan berdasarkan logika Anda
            if (musicSource != null && !musicSource.isPlaying)
            {
                musicSource.PlayOneShot(theme);
                musicSource.loop = true;
            }
        }
    }


    // Set BGM and SFX in the settings menu
    private void Start()
    {
        LoadVolume();
    }

    public void SetMusicSliderVolume(float value)
    {
        myMixer.SetFloat("Music", Mathf.Log10(value) * 20 + 5);
        PlayerPrefs.SetFloat(MUSIC_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }
    public void SetSFXSliderVolume(float value)
    {
        myMixer.SetFloat("SFX", Mathf.Log10(value) * 20 - 10);
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, value);
        PlayerPrefs.Save();
    }
    public float GetBGM()
    {
        return PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f);
    }
    public float GetSFX()
    {
        return PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
    }
    private void LoadVolume()
    {
        float savedBGMVolume = GetBGM();
        float savedSFXVolume = GetSFX();
        myMixer.SetFloat("SFX", Mathf.Log10(savedBGMVolume) * 20 - 10);
        myMixer.SetFloat("Music", Mathf.Log10(savedSFXVolume) * 20 + 5);
    }

    // Turn On and turn Off MasterAudio
    public void TurnOnAudio()
    {
        float savedVolume = PlayerPrefs.GetFloat(MUSIC_VOLUME_KEY, 1f); // Default volume is 1
        myMixer.SetFloat("MasterVolume", Mathf.Log10(savedVolume) * 20); // Set master volume to the saved volume level
    }


    public void TurnOffAudio()
    {
        myMixer.SetFloat("MasterVolume", -80); // Set master volume to -80 dB (mute level)
    }


    private const string MUSIC_VOLUME_KEY = "MusicVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";

    //DonDestroy
    void Awake()
    {
        // Apply Singleton pattern
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        LoadVolume();
        musicSource.PlayOneShot(theme);
        musicSource.loop = true;
    }

    private void Update()
    {
        string activeSceneName = SceneManager.GetActiveScene().name;

        if (activeSceneName != currentLevel)
        {
            if (activeSceneName == "NewLevelOne")
            {
                if (musicSource.isPlaying)
                {
                    musicSource.Stop();
                }
                musicSource.clip = inGameTheme;
                musicSource.volume = 1;
                musicSource.Play();
                player = PlayerController.Instance;
            }
            else if (activeSceneName == "Intro Komik")
            {
                musicSource.Stop();
            }
            else if (musicSource.clip != theme)
            {
                musicSource.Stop();
                musicSource.volume = 0.125f;
                musicSource.clip = theme;
                musicSource.Play();
            }
            currentLevel = activeSceneName;
        }

        if (Time.timeScale != 0)
        {
            if (player != null)
            {
                if (player.playerState == PlayerState.Idle)
                    audioState = player.playerState;
                if (player.playerData.isMan)
                {
                    PlayerManSFX();
                }
                else
                {
                    PlayerWomanSFX();
                }
                StateControl();
            }
            else
            {
                playerSFXSource.Stop();
                playerSFXMovementSource.Stop();
                playerSFXAttackSource.Stop();
                playerSFXDashSource.Stop();
            }
        }
        else
        {
            SFXSource.Stop();

        }

        if (GameProgression.Instance != null)
        {
            if (GameProgression.Instance.Percentage == 100 && !IsBossPlay)
            {
                musicSource.clip = bossTheme;
                musicSource.volume = 0.15f;
                musicSource.Play();
                IsBossPlay = true;
            }
        }
    }



    //Reset Player Control
    public void ResetPlayerController()
    {
        currentLevel = "";
        player = null;
    }

    public void PlaySFX(AudioClip clip)
    {
        if (clip != null && SFXSource != null)
        {
            if (clip == gate)
            {
                SFXSource.pitch = 1f;
                SFXSource.volume = 0.4f;
                SFXSource.PlayOneShot(clip);
            }
            else if (clip == gateClose)
            {
                SFXSource.pitch = 1.2f;
                SFXSource.volume = 0.4f;
                SFXSource.PlayOneShot(clip);
            }
            else
            {
                SFXSource.volume = 1f;
                SFXSource.pitch = 1f;
                SFXSource.PlayOneShot(clip);
            }
        }
    }
    //Chest
    public void PlayChest()
    {
        if (environmentSFXSource != null)
        {
            environmentSFXSource.pitch = 1f;
            environmentSFXSource.PlayOneShot(chest);
        }
    }
    public void PlayTower()
    {
        if (environmentSFXSource != null)
        {
            environmentSFXSource.pitch = 0.8f;
            environmentSFXSource.PlayOneShot(tower);
        }
    }


    public void PlayBoxGettingHit(int health)
    {
        environmentSFXSource.pitch = 1f;

        if (health >= 0 && environmentSFXSource != null)
        {
            environmentSFXSource.PlayOneShot(boxGettingHit[health]);
        }
        if (health == 0)
        {
            environmentSFXSource.PlayOneShot(boxCrack);
        }
    }


    // Methods for UI SFX
    public void PlayButtonClickSound()
    {
        PlaySFX(buttonClick);
    }

    public void PlayButtonHoverSound()
    {
        PlaySFX(buttonHover);
    }



    private void StateControl()
    {
        if (playerSFXAttackSource.isPlaying && (audioState == PlayerState.Run || audioState == PlayerState.Dash || audioState == PlayerState.Parry || audioState == PlayerState.Defeated))
        {
            playerSFXAttackSource.Stop();
        }
    }

    public void PlayEnemyAlertedSound(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Creep:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(creepAlertedClip);
                break;
            case EnemyType.Medium1:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium1AlertedClip);
                break;
            case EnemyType.Medium2:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium2AlertedClip);
                break;
        }
    }

    public void PlayEnemyAttackSound(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Creep:
                if (creepCount == creepAttackClip.Length)
                {
                    creepCount = 0;
                }
                // Debug.Log(creepAttackClip[creepCount].name);
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(creepAttackClip[creepCount]);
                creepCount++;
                break;
            case EnemyType.Medium1:
                if (medium1Count == medium1AttackClip.Length)
                {
                    medium1Count = 0;
                }
                // Debug.Log(medium1AttackClip[medium1Count].name);
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium1AttackClip[medium1Count]);
                medium1Count++;
                break;
            case EnemyType.Medium2:
                if (medium2Count == medium2AttackClip.Length)
                {
                    medium2Count = 0;
                }
                // Debug.Log(medium2AttackClip[medium2Count].name);
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium2AttackClip[medium2Count]);
                medium2Count++;
                break;
        }
    }

    public void PlayBossSound(AudioClip clip)
    {
        enemyAudioSource.pitch = 1f;
        enemyAudioSource.PlayOneShot(clip);
    }

    int zz = 0;

    public void PlayBossHurtSound()
    {
        enemyAudioSource.pitch = 1f;
        if (zz == bossHurtClip.Length)
        {
            zz = 0;
        }
        // Debug.Log(playerManHurtSfx[j].name);
        enemyAudioSource.PlayOneShot(bossHurtClip[zz]);
        zz++;
    }
    int xx = 0;
    public void PlayBossMovement()
    {
        if (Boss3Controller.Instance.currentState == Boss3State.Chase && !enemyAudioSource.isPlaying)
        {
            if (xx == bossMovementClip.Length)
            {
                xx = 0;
            }
            enemyAudioSource.pitch = 0.6f;
            enemyAudioSource.PlayOneShot(bossMovementClip[xx]);
            xx++;
        }
    }
    public void PlayEnemyGetHitSound(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Creep:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(creepGetHitClip);
                break;
            case EnemyType.Medium1:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium1GetHitClip);
                break;
            case EnemyType.Medium2:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium2GetHitClip);
                break;
        }
    }

    public void PlayEnemyDieSound(EnemyType enemyType)
    {
        switch (enemyType)
        {
            case EnemyType.Creep:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(creepDieClip);
                break;
            case EnemyType.Medium1:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium1DieClip);
                break;
            case EnemyType.Medium2:
                enemyAudioSource.pitch = 1f;
                enemyAudioSource.PlayOneShot(medium2DieClip);
                break;
        }
    }

    #region PLAYER MAN



    private void PlayerManSFX()
    {
        #region RUN

        playerSFXMovementSource.volume = player.Animator.GetFloat("Speed");
        playerSFXMovementSource.pitch = player.Animator.GetFloat("Speed") * 0.8f;

        if (player.playerState == PlayerState.Run && !playerSFXMovementSource.isPlaying)
        {
            if (i == playerManMovementSfx.Length)
            {
                i = 0;
                System.Random random = new System.Random();

                for (int k = playerManMovementSfx.Length - 1; k > 0; k--)
                {
                    int j = random.Next(0, k + 1);
                    (playerManMovementSfx[j], playerManMovementSfx[k]) = (playerManMovementSfx[k], playerManMovementSfx[j]);
                }
            }
            // Debug.Log(playerManMovementSfx[i].name);
            playerSFXMovementSource.PlayOneShot(playerManMovementSfx[i]);
            i++;
            audioState = player.playerState;
        }
        #endregion

        #region DASH
        if (player.playerState == PlayerState.Dash && audioState != player.playerState)
        {
            playerSFXSource.pitch = 0.8f;
            playerSFXDashSource.PlayOneShot(dashMan);
            audioState = player.playerState;
        }
        #endregion

        #region PARRY
        if (player.canParry && player.playerState == PlayerState.Parry && audioState != player.playerState)
        {
            playerParrySource.PlayOneShot(parry);
            audioState = player.playerState;
        }
        #endregion

        #region PARRY SUCCESS
        if (player.playerState == PlayerState.ParrySuccess && audioState != player.playerState)
        {
            playerParrySource.PlayOneShot(parrySuccess);
            audioState = player.playerState;
        }
        #endregion

        #region DEFEATED
        if (player.playerState == PlayerState.Defeated && audioState != player.playerState)
        {
            playerSFXSource.pitch = 1;
            playerSFXSource.PlayOneShot(deathWoman);
            audioState = player.playerState;
        }
        #endregion

        #region HURT
        if (player.playerState == PlayerState.Hurt && !playerSFXSource.isPlaying)
        {
            playerSFXSource.pitch = 0.8f;
            if (j == playerManHurtSfx.Length)
            {
                j = 1;
            }
            // Debug.Log(playerManHurtSfx[j].name);
            playerSFXSource.PlayOneShot(playerManHurtSfx[j]);
            j++;
            audioState = player.playerState;
        }
        #endregion

        #region ATTACK CYCLE
        if (player.canAttack && player.playerState == PlayerState.Attack1 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 4f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerManCombatSfx[1]);
            audioState = player.playerState;
        }
        if (player.canAttack && player.playerState == PlayerState.Attack2 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 4f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerManCombatSfx[2]);
            audioState = player.playerState;
        }
        if (player.canAttack && player.playerState == PlayerState.Attack3 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 4f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerManCombatSfx[3]);
            audioState = player.playerState;
        }
        #endregion

        #region SKILL ATTACK
        if (player.playerState == PlayerState.SkillAttack && audioState != player.playerState)
        {
            playerSFXAttackSource.Stop();
            playerSFXAttackSource.pitch = 1.25f;
            playerSFXAttackSource.PlayOneShot(playerManCombatSfx[4]);
            audioState = player.playerState;
        }
        #endregion
        #region DASH ATTACK
        if (player.playerState == PlayerState.DashAttack && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 2.25f;
            playerSFXAttackSource.PlayOneShot(playerManCombatSfx[0]);
            audioState = player.playerState;
        }
        #endregion
    }
    #endregion


    #region PLAYER WOMAN

    private void PlayerWomanSFX()
    {
        #region RUN

        playerSFXMovementSource.volume = player.Animator.GetFloat("Speed");
        playerSFXMovementSource.pitch = player.Animator.GetFloat("Speed") * 0.6f;

        if (player.playerState == PlayerState.Run && !playerSFXMovementSource.isPlaying)
        {
            if (i == playerWomanMovementSfx.Length)
            {
                i = 0;
                System.Random random = new System.Random();

                for (int k = playerWomanMovementSfx.Length - 1; k > 0; k--)
                {
                    int j = random.Next(0, k + 1);
                    (playerWomanMovementSfx[j], playerWomanMovementSfx[k]) = (playerWomanMovementSfx[k], playerWomanMovementSfx[j]);
                }
            }
            // Debug.Log(playerWomanMovementSfx[i].name);
            playerSFXMovementSource.PlayOneShot(playerWomanMovementSfx[i]);
            i++;
            audioState = player.playerState;
        }
        #endregion

        #region DASH
        if (player.playerState == PlayerState.Dash && audioState != player.playerState)
        {
            playerSFXSource.pitch = 0.6f;
            playerSFXDashSource.PlayOneShot(dashWoman);
            audioState = player.playerState;
        }
        #endregion

        #region PARRY
        if (player.canParry && player.playerState == PlayerState.Parry && audioState != player.playerState)
        {
            playerParrySource.PlayOneShot(parry);
            audioState = player.playerState;
        }
        #endregion

        #region PARRY SUCCESS
        if (player.playerState == PlayerState.ParrySuccess && audioState != player.playerState)
        {
            playerParrySource.PlayOneShot(parrySuccess);
            audioState = player.playerState;
        }
        #endregion

        #region DEFEATED
        if (player.playerState == PlayerState.Defeated && audioState != player.playerState)
        {
            playerSFXSource.pitch = 1;
            playerSFXSource.PlayOneShot(deathWoman);
            audioState = player.playerState;
        }
        #endregion

        #region HURT
        if (player.playerState == PlayerState.Hurt && !playerSFXSource.isPlaying)
        {
            playerSFXSource.pitch = 0.8f;
            if (j == playerWomanHurtSfx.Length)
            {
                j = 1;
            }
            // Debug.Log(playerWomanHurtSfx[j].name);
            playerSFXSource.PlayOneShot(playerWomanHurtSfx[j]);
            j++;
            audioState = player.playerState;
        }
        #endregion

        #region ATTACK CYCLE
        if (player.canAttack && player.playerState == PlayerState.Attack1 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 2.25f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerWomanCombatSfx[1]);
            audioState = player.playerState;
        }
        if (player.canAttack && player.playerState == PlayerState.Attack2 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 2f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerWomanCombatSfx[2]);
            audioState = player.playerState;
        }
        if (player.canAttack && player.playerState == PlayerState.Attack3 && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 1.25f;
            audioState = player.playerState;
            playerSFXAttackSource.PlayOneShot(playerWomanCombatSfx[3]);
            audioState = player.playerState;
        }
        #endregion

        #region SKILL ATTACK
        if (player.playerState == PlayerState.SkillAttack && audioState != player.playerState)
        {
            playerSFXAttackSource.Stop();
            playerSFXAttackSource.pitch = 1.25f;
            playerSFXAttackSource.PlayOneShot(playerWomanCombatSfx[4]);
            audioState = player.playerState;
        }
        #endregion
        #region DASH ATTACK
        if (player.playerState == PlayerState.DashAttack && audioState != player.playerState)
        {
            playerSFXAttackSource.pitch = 2.25f;
            playerSFXAttackSource.PlayOneShot(playerWomanCombatSfx[0]);
            audioState = player.playerState;
        }
        #endregion
    }
    #endregion
}
