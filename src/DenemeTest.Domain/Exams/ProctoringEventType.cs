namespace DenemeTest.Exams;

public enum ProctoringEventType
{
    FocusLost = 1,       // sekme/odak kaybı
    MultipleTabs = 2,    // birden fazla sekme tespiti
    ScreenShareStop = 3, // ekran kaydı/kamera durdu
    ShortcutAltTab = 4,  // alt+tab benzeri kısayol – pratikte focus lost ile aynı yakalanır
    Other = 99
}
