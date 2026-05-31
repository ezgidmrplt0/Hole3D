using System;
using System.Collections.Generic;
using UnityEngine;
using Firebase;
using Firebase.Analytics;
using Firebase.Crashlytics;
using Firebase.Firestore;
using Firebase.Extensions;

public class FirebaseManager : MonoBehaviour
{
    public static FirebaseManager Instance { get; private set; }

    private DependencyStatus dependencyStatus = DependencyStatus.UnavailableOther;
    
    // Firestore veritabanı referansı
    public FirebaseFirestore db { get; private set; }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Sahneler arası geçişte yok olmasını engeller
            if (GetComponent<FirestoreAnalytics>() == null)
            {
                gameObject.AddComponent<FirestoreAnalytics>();
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        // Firebase bağımlılıklarını kontrol et ve gerekirse düzelt
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task => {
            dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                InitializeFirebase();
            }
            else
            {
                Debug.LogError("Firebase başlatılamadı. Eksik bağımlılık: " + dependencyStatus);
            }
        });
    }

    private void InitializeFirebase()
    {
        Debug.Log("Firebase başarıyla başlatıldı!");
        
        // --- 1. Analytics Başlatma ---
        FirebaseAnalytics.SetAnalyticsCollectionEnabled(true);
        FirebaseAnalytics.LogEvent(FirebaseAnalytics.EventAppOpen);

        // --- 2. Crashlytics Başlatma ---
        Crashlytics.ReportUncaughtExceptionsAsFatal = true;

        // --- 3. Firestore Başlatma ---
        db = FirebaseFirestore.DefaultInstance;

        FirestoreAnalytics.Instance?.Initialize();

        // --- 4. Çalıştığını Test Etme (Opsiyonel) ---
        // TestFirebaseBağlantısı();
    }

    private void TestFirebaseBağlantısı()
    {
        Debug.Log("Firestore bağlantısı test ediliyor...");
        var testData = new Dictionary<string, object>
        {
            { "mesaj", "Firebase Unity ile başarıyla bağlandı!" },
            { "zaman", Timestamp.GetCurrentTimestamp() }
        };

        SaveData("TestKoleksiyonu", "BaglantıTesti", testData);
    }

    // --- ANALYTICS FONKSİYONLARI ---
    
    public void LogCustomEvent(string eventName)
    {
        if (dependencyStatus == DependencyStatus.Available)
        {
            FirebaseAnalytics.LogEvent(eventName);
            Debug.Log($"Firebase Event gönderildi: {eventName}");
        }
    }

    // --- LEVEL OLAYLARI ---

    public void LogLevelStart(int levelIndex)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_start", new Parameter("level_index", levelIndex));
        FirestoreAnalytics.Instance?.LogLevelStart(levelIndex);
    }

    public void LogLevelComplete(int levelIndex, float durationSeconds)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_complete",
            new Parameter("level_index", levelIndex),
            new Parameter("duration_seconds", durationSeconds));

        FirestoreAnalytics.Instance?.LogLevelComplete(levelIndex, durationSeconds);
    }

    public void LogLevelFail(int levelIndex, float durationSeconds)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_fail",
            new Parameter("level_index", levelIndex),
            new Parameter("duration_seconds", durationSeconds));

        FirestoreAnalytics.Instance?.LogLevelFail(levelIndex, durationSeconds);
    }

    public void LogLevelRetry(int levelIndex)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_retry", new Parameter("level_index", levelIndex));
        FirestoreAnalytics.Instance?.LogLevelRetry(levelIndex);
    }

    public void LogLevelReset(int levelIndex)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_reset", new Parameter("level_index", levelIndex));
        FirestoreAnalytics.Instance?.LogLevelReset(levelIndex);
    }

    public void LogLevelQuit(int levelIndex, float durationSeconds)
    {
        if (dependencyStatus != DependencyStatus.Available) return;

        FirebaseAnalytics.LogEvent("level_quit",
            new Parameter("level_index", levelIndex),
            new Parameter("duration_seconds", durationSeconds));

        FirestoreAnalytics.Instance?.LogLevelQuit(levelIndex, durationSeconds);
    }

    // --- FIRESTORE FONKSİYONLARI ---

    // Veri kaydetme (Dictionary kullanarak)
    public void SaveData(string collectionPath, string documentId, Dictionary<string, object> data)
    {
        if (db != null)
        {
            DocumentReference docRef = db.Collection(collectionPath).Document(documentId);
            docRef.SetAsync(data).ContinueWithOnMainThread(task => {
                if (task.IsCompleted)
                {
                    Debug.Log($"Firestore'a veri kaydedildi: {collectionPath}/{documentId}");
                }
                else
                {
                    Debug.LogError("Firestore veri kaydetme hatası: " + task.Exception);
                }
            });
        }
    }

    // Veri çekme (Dictionary olarak döndürür)
    public void LoadData(string collectionPath, string documentId, Action<Dictionary<string, object>> onDataLoaded)
    {
        if (db != null)
        {
            DocumentReference docRef = db.Collection(collectionPath).Document(documentId);
            docRef.GetSnapshotAsync().ContinueWithOnMainThread(task => {
                if (task.IsFaulted)
                {
                    Debug.LogError("Firestore veri çekme hatası: " + task.Exception);
                    onDataLoaded?.Invoke(null);
                }
                else if (task.IsCompleted)
                {
                    DocumentSnapshot snapshot = task.Result;
                    if (snapshot.Exists)
                    {
                        onDataLoaded?.Invoke(snapshot.ToDictionary());
                    }
                    else
                    {
                        onDataLoaded?.Invoke(null); // Veri yok
                    }
                }
            });
        }
    }
}
