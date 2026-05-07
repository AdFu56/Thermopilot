Imports System
Imports System.Collections.Generic
Imports System.Windows.Forms
Imports Microsoft.VisualBasic
Imports System.IO
Imports System.Drawing
Imports System.Linq

' ═══════════════════════════════════════════════════════════════════════════════
'  PARAMÈTRES DÉBITMÈTRE
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Paramètres complets pour la conversion courant → débit.
''' Câblage : débitmètre 4-20mA → résistance shunt → Keithley mesure V.
'''
''' Formule :
'''   I (mA) = V_mesurée / R_shunt × 1000
'''   I_norm = (I - I_min) / (I_max - I_min)   [0..1]
'''   Qv     = Qv_min + I_norm × (Qv_max - Qv_min)
''' </summary>
Public Class ParamDebitmetre

    ' Résistance shunt (Ω) — typiquement 250 Ω
    Public Property RShuntOhm As Double = 250.0

    ' Plage courant capteur (mA) — fixe 4-20mA
    Public Property IminMA As Double = 4.0
    Public Property ImaxMA As Double = 20.0

    ' Plage débit physique
    Public Property QvMin   As Double = 0.0
    Public Property QvMax   As Double = 100.0
    Public Property Unite   As String = "L/h"

    ' Tension d'alimentation du capteur (informatif, non utilisé dans le calcul)
    Public Property TensionAlimV As Double = 24.0

    ''' <summary>
    ''' Convertit la tension mesurée par le Keithley en débit.
    ''' </summary>
    Public Function VersDebit(tensionMesureeV As Double) As Double
        If RShuntOhm <= 0 Then Return Double.NaN
        Dim iMA As Double = (tensionMesureeV / RShuntOhm) * 1000.0
        Dim plage = ImaxMA - IminMA
        If plage <= 0 Then Return Double.NaN
        Dim norm As Double = Math.Max(0.0, Math.Min(1.0, (iMA - IminMA) / plage))
        Return QvMin + norm * (QvMax - QvMin)
    End Function

    ''' <summary>Courant correspondant à la tension mesurée (pour affichage).</summary>
    Public Function VersCourantMA(tensionMesureeV As Double) As Double
        If RShuntOhm <= 0 Then Return Double.NaN
        Return (tensionMesureeV / RShuntOhm) * 1000.0
    End Function

    Public Sub SauverDansConfig(cfg As ConfigManager, cle As String)
        Dim s = ConfigManager.SEC_VOIES
        cfg.Set_(s, cle & "_RShunt",     RShuntOhm)
        cfg.Set_(s, cle & "_IminMA",     IminMA)
        cfg.Set_(s, cle & "_ImaxMA",     ImaxMA)
        cfg.Set_(s, cle & "_QvMin",      QvMin)
        cfg.Set_(s, cle & "_QvMax",      QvMax)
        cfg.Set_(s, cle & "_Unite",      Unite)
        cfg.Set_(s, cle & "_AlimV",      TensionAlimV)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager, cle As String)
        Dim s = ConfigManager.SEC_VOIES
        RShuntOhm    = cfg.GetDouble(s, cle & "_RShunt",  250.0)
        IminMA       = cfg.GetDouble(s, cle & "_IminMA",  4.0)
        ImaxMA       = cfg.GetDouble(s, cle & "_ImaxMA",  20.0)
        QvMin        = cfg.GetDouble(s, cle & "_QvMin",   0.0)
        QvMax        = cfg.GetDouble(s, cle & "_QvMax",   100.0)
        Unite        = cfg.Get_(s,     cle & "_Unite",    "L/h")
        TensionAlimV = cfg.GetDouble(s, cle & "_AlimV",  24.0)
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  SORTIE ANALOGIQUE (123, 124, 223, 224)
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Représente une sortie tension analogique du Keithley (voies 123, 124, 223, 224).
''' Trois modes :
'''   - Booleen    : 0V (OFF) ou +Amplitude V (ON) — piloté par case à cocher
'''   - Analogique : 0 à +Amplitude V — tension positive variable
'''   - AnalogiqueFull : −Amplitude à +Amplitude V — actionneur bidirectionnel (V3V, etc.)
''' </summary>
Public Class SortieAnalogique

    Public Enum ModePilotage
        Booleen        ' 0V ou +Us — checkbox dans chronogramme
        Analogique     ' 0..+Us V — valeur numérique par étape
        AnalogiqueFull ' −Us..+Us V — actionneur bidirectionnel (V3V, etc.)
    End Enum

    Public Property Numero       As Integer       ' 123, 124, 223, 224
    Public Property Nom          As String = ""
    Public Property Active       As Boolean = False
    Public Property Mode         As ModePilotage = ModePilotage.Booleen
    ''' <summary>
    ''' Us : tension maximale (V).
    ''' Booléen/Analogique : sortie de 0 à +Us.
    ''' Analogique [-Us;Us] : sortie de −Us à +Us.
    ''' Plage physique module 7706 : 0..12 V (Booléen/Analogique) ou ±12 V (Analogique [-Us;Us]).
    ''' </summary>
    Public Property Amplitude    As Double = 5.0
    Public Property SeuilOnV     As Double = 2.5  ' Seuil ON pour mode Booléen (et graphique)
    Public Property SecuriteDebit As Boolean = False

    ' État courant
    Public Property TensionV     As Double = 0.0
    Public Property Horodatage   As DateTime = DateTime.MinValue

    ' ── Compatibilité ascendante : UMax → Amplitude ──────────────────────────
    ''' <summary>Alias conservé pour compatibilité avec le code existant.</summary>
    Public Property UMax As Double
        Get
            Return Amplitude
        End Get
        Set(value As Double)
            Amplitude = value
        End Set
    End Property

    Public ReadOnly Property EstOn As Boolean
        Get
            Return TensionV >= SeuilOnV
        End Get
    End Property

    Public ReadOnly Property ValeurGraphiqueB As Double
        Get
            If Mode = ModePilotage.Analogique OrElse Mode = ModePilotage.AnalogiqueFull Then
                Return TensionV
            Else
                Return If(EstOn, 1.0, 0.0)
            End If
        End Get
    End Property

    ' Alias historique pour compatibilité
    Public ReadOnly Property ValeurGraphique As Double
        Get
            Return ValeurGraphiqueB
        End Get
    End Property

    ''' <summary>Tension minimale autorisée selon le mode.</summary>
    Public ReadOnly Property TensionMin As Double
        Get
            Return If(Mode = ModePilotage.AnalogiqueFull, -Amplitude, 0.0)
        End Get
    End Property

    ''' <summary>Tension maximale autorisée selon le mode.</summary>
    Public ReadOnly Property TensionMax As Double
        Get
            Return Amplitude
        End Get
    End Property

    ''' <summary>Libellé court du mode pour l'affichage.</summary>
    Public ReadOnly Property LibelleMode As String
        Get
            Select Case Mode
                Case ModePilotage.Booleen       : Return "Booléen (0/+Us)"
                Case ModePilotage.Analogique    : Return "Analogique [0;Us]"
                Case ModePilotage.AnalogiqueFull: Return "Analogique [-Us;Us]"
                Case Else                    : Return "?"
            End Select
        End Get
    End Property

    Public ReadOnly Property CleIni As String
        Get
            Return "Sortie" & Numero.ToString()
        End Get
    End Property

    Public Sub SauverDansConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_VOIES
        cfg.Set_(s, CleIni & "_Nom",       Nom)
        cfg.Set_(s, CleIni & "_Active",    Active)
        cfg.Set_(s, CleIni & "_Mode",      CInt(Mode))
        cfg.Set_(s, CleIni & "_UMax",      Amplitude)   ' clé inchangée pour compatibilité
        cfg.Set_(s, CleIni & "_Seuil",     SeuilOnV)
        cfg.Set_(s, CleIni & "_SecuDebit", SecuriteDebit)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_VOIES
        Dim nomSave = cfg.Get_(s, CleIni & "_Nom", "")
        If nomSave <> "" Then Nom = nomSave
        Active        = cfg.GetBool(s,   CleIni & "_Active",    False)
        Mode          = CType(cfg.GetInt(s, CleIni & "_Mode",   0), ModePilotage)
        Amplitude     = cfg.GetDouble(s, CleIni & "_UMax",      5.0)
        SeuilOnV      = cfg.GetDouble(s, CleIni & "_Seuil",     2.5)
        SecuriteDebit = cfg.GetBool(s,   CleIni & "_SecuDebit", False)
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  VOIE DE MESURE
' ═══════════════════════════════════════════════════════════════════════════════

Public Class VoieMesure

    Public Enum TypeVoie
        Temperature
        Debit
    End Enum

    ' ─── Configuration de base ────────────────────────────────────────────────

    Public Property Numero      As Integer
    Public Property Nom         As String = ""
    Public Property Description As String = ""
    Public Property Type        As TypeVoie
    Public Property Active      As Boolean = True

    ' Paramètres débitmètre (utilisés si Type = Debit)
    Public Property ParamDebit As New ParamDebitmetre()

    ' Propriétés de compatibilité (lues par ancien code)
    Public Property DebitMin As Double
        Get
            Return ParamDebit.QvMin
        End Get
        Set(value As Double)
            ParamDebit.QvMin = value
        End Set
    End Property

    Public Property DebitMax As Double
        Get
            Return ParamDebit.QvMax
        End Get
        Set(value As Double)
            ParamDebit.QvMax = value
        End Set
    End Property

    Public ReadOnly Property Unite As String
        Get
            If Type = TypeVoie.Temperature Then
                Return "°C"
            Else
                Return ParamDebit.Unite
            End If
        End Get
    End Property

    ' ─── Alarmes ──────────────────────────────────────────────────────────────

    Public Property AlarmeActive   As Boolean = False
    Public Property SeuilBas       As Double  = Double.NaN
    Public Property SeuilHaut      As Double  = Double.NaN
    Public Property HysteresisK    As Double  = 0.5
    Public Property RelaisAlarmeHaut As Nullable(Of Relais.NomRelais) = Nothing
    Public Property RelaisAlarmeBas  As Nullable(Of Relais.NomRelais) = Nothing
    ''' <summary>
    ''' Si True, cette voie sert de mesure de débit de sécurité.
    ''' Si sa valeur sort de [SeuilBas, SeuilHaut], les sorties marquées
    ''' SecuriteDebit sont forcées à 0V par le moteur du chronogramme.
    ''' </summary>
    Public Property SurveillanceDebit As Boolean = False

    Public Property EnAlarmeHaute As Boolean = False
    Public Property EnAlarmeBasse As Boolean = False

    Public ReadOnly Property EnAlarme As Boolean
        Get
            Return EnAlarmeHaute OrElse EnAlarmeBasse
        End Get
    End Property

    Public ReadOnly Property MessageAlarme As String
        Get
            If EnAlarmeHaute Then
                Return String.Format("⚠ {0} : {1:F2} {2} > {3:F2}", Nom, Valeur, Unite, SeuilHaut)
            End If
            If EnAlarmeBasse Then
                Return String.Format("⚠ {0} : {1:F2} {2} < {3:F2}", Nom, Valeur, Unite, SeuilBas)
            End If
            Return ""
        End Get
    End Property

    ' ─── Valeur courante ──────────────────────────────────────────────────────

    Public Property ValeurBrute As Double   = Double.NaN
    Public Property Valeur      As Double   = Double.NaN
    Public Property Horodatage  As DateTime = DateTime.MinValue
    Public Property EnErreur    As Boolean  = False

    ' ─── Conversion ──────────────────────────────────────────────────────────

    Public Sub CalculerValeur()
        If Double.IsNaN(ValeurBrute) OrElse EnErreur Then
            Valeur = Double.NaN
            EnAlarmeHaute = False
            EnAlarmeBasse = False
            Return
        End If

        Select Case Type
            Case TypeVoie.Temperature
                Valeur = ValeurBrute

            Case TypeVoie.Debit
                ' V mesurée → courant → débit via ParamDebit
                Valeur = ParamDebit.VersDebit(ValeurBrute)
                If Double.IsNaN(Valeur) Then
                    EnErreur = True
                    Return
                End If
        End Select

        EvaluerAlarmes()
    End Sub

    Private Sub EvaluerAlarmes()
        If Not AlarmeActive OrElse Double.IsNaN(Valeur) Then
            EnAlarmeHaute = False
            EnAlarmeBasse = False
            Return
        End If
        If Not Double.IsNaN(SeuilHaut) Then
            If Valeur > SeuilHaut Then EnAlarmeHaute = True
            If Valeur < SeuilHaut - HysteresisK Then EnAlarmeHaute = False
        End If
        If Not Double.IsNaN(SeuilBas) Then
            If Valeur < SeuilBas Then EnAlarmeBasse = True
            If Valeur > SeuilBas + HysteresisK Then EnAlarmeBasse = False
        End If
    End Sub

    Public Function DebitSuffisant(seuilLH As Double) As Boolean
        If Type <> TypeVoie.Debit Then Return True
        If Double.IsNaN(Valeur) Then Return False
        Return Valeur >= seuilLH
    End Function

    ' ─── Sérialisation ────────────────────────────────────────────────────────

    Public ReadOnly Property CleIni As String
        Get
            Return "Voie" & Numero.ToString()
        End Get
    End Property

    Public Sub SauverDansConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_VOIES
        cfg.Set_(s, CleIni & "_Nom",          Nom)
        cfg.Set_(s, CleIni & "_Desc",         Description)
        cfg.Set_(s, CleIni & "_Active",       Active)
        cfg.Set_(s, CleIni & "_AlarmeActive", AlarmeActive)
        cfg.Set_(s, CleIni & "_Hysteresis",   HysteresisK)
        cfg.Set_(s, CleIni & "_SeuilBas",
            If(Double.IsNaN(SeuilBas), "", SeuilBas.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        cfg.Set_(s, CleIni & "_SeuilHaut",
            If(Double.IsNaN(SeuilHaut), "", SeuilHaut.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        cfg.Set_(s, CleIni & "_RelaisHaut",
            If(RelaisAlarmeHaut.HasValue, CInt(RelaisAlarmeHaut.Value).ToString(), ""))
        cfg.Set_(s, CleIni & "_RelaisBas",
            If(RelaisAlarmeBas.HasValue, CInt(RelaisAlarmeBas.Value).ToString(), ""))
        ' Paramètres débitmètre
        If Type = TypeVoie.Debit Then
            ParamDebit.SauverDansConfig(cfg, CleIni)
        End If
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_VOIES
        Dim nomSave = cfg.Get_(s, CleIni & "_Nom", "")
        If nomSave <> "" Then Nom = nomSave
        Description  = cfg.Get_(s, CleIni & "_Desc", "")
        Active       = cfg.GetBool(s, CleIni & "_Active", True)
        AlarmeActive = cfg.GetBool(s, CleIni & "_AlarmeActive", False)
        HysteresisK  = cfg.GetDouble(s, CleIni & "_Hysteresis", 0.5)

        Dim sb = cfg.Get_(s, CleIni & "_SeuilBas", "")
        Dim sh = cfg.Get_(s, CleIni & "_SeuilHaut", "")
        SeuilBas  = If(sb = "", Double.NaN,
            Double.Parse(sb, System.Globalization.CultureInfo.InvariantCulture))
        SeuilHaut = If(sh = "", Double.NaN,
            Double.Parse(sh, System.Globalization.CultureInfo.InvariantCulture))

        Dim rh = cfg.Get_(s, CleIni & "_RelaisHaut", "")
        Dim rb = cfg.Get_(s, CleIni & "_RelaisBas",  "")
        If rh = "" Then
            RelaisAlarmeHaut = Nothing
        Else
            RelaisAlarmeHaut = New Nullable(Of Relais.NomRelais)(CType(CInt(rh), Relais.NomRelais))
        End If
        If rb = "" Then
            RelaisAlarmeBas = Nothing
        Else
            RelaisAlarmeBas = New Nullable(Of Relais.NomRelais)(CType(CInt(rb), Relais.NomRelais))
        End If
        ' Paramètres débitmètre
        If Type = TypeVoie.Debit Then
            ParamDebit.ChargerDepuisConfig(cfg, CleIni)
        End If
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  GESTION DES VOIES
' ═══════════════════════════════════════════════════════════════════════════════

Public Class GestionVoies

    Public Property Voies   As New List(Of VoieMesure)
    Public Property Sorties As New List(Of SortieAnalogique)

    Public Event AlarmeChangee(sender As Object, voie As VoieMesure, enAlarme As Boolean)

    ' ─── Ajout voies ──────────────────────────────────────────────────────────

    Public Sub AjouterVoieTemp(numero As Integer, nom As String)
        Voies.Add(New VoieMesure With {
            .Numero = numero,
            .Nom    = nom,
            .Type   = VoieMesure.TypeVoie.Temperature
        })
    End Sub

    Public Sub AjouterVoieDebit(numero As Integer, nom As String,
                                 debitMin As Double, debitMax As Double)
        Dim v As New VoieMesure With {
            .Numero = numero,
            .Nom    = nom,
            .Type   = VoieMesure.TypeVoie.Debit
        }
        v.ParamDebit.QvMin = debitMin
        v.ParamDebit.QvMax = debitMax
        Voies.Add(v)
    End Sub

    Public Sub AjouterVoieDebitComplet(numero As Integer, nom As String,
                                        param As ParamDebitmetre)
        Dim v As New VoieMesure With {
            .Numero     = numero,
            .Nom        = nom,
            .Type       = VoieMesure.TypeVoie.Debit,
            .ParamDebit = param
        }
        Voies.Add(v)
    End Sub

    ' ─── Parse réponse Keithley ───────────────────────────────────────────────

    Public Sub ParseReponse(reponse As String, horodatage As DateTime)
        If String.IsNullOrWhiteSpace(reponse) Then Return
        Dim tokens() = reponse.Split(","c)
        Dim actives  = Voies.Where(Function(v) v.Active).ToList()

        For i As Integer = 0 To Math.Min(tokens.Length - 1, actives.Count - 1)
            Dim v           = actives(i)
            Dim etaitAlarme = v.EnAlarme
            v.Horodatage = horodatage
            Try
                Dim val As Double = Double.Parse(tokens(i).Trim(),
                    System.Globalization.CultureInfo.InvariantCulture)
                If Math.Abs(val) > 9.0E+30 Then
                    v.EnErreur    = True
                    v.ValeurBrute = Double.NaN
                Else
                    v.EnErreur    = False
                    v.ValeurBrute = val
                End If
                v.CalculerValeur()
            Catch
                v.EnErreur    = True
                v.ValeurBrute = Double.NaN
                v.Valeur      = Double.NaN
            End Try
            If v.EnAlarme <> etaitAlarme Then
                RaiseEvent AlarmeChangee(Me, v, v.EnAlarme)
            End If
        Next
    End Sub

    ' ─── Accès ────────────────────────────────────────────────────────────────

    Public Function TrouverVoie(numero As Integer) As VoieMesure
        Return Voies.FirstOrDefault(Function(v) v.Numero = numero)
    End Function

    Public Function TrouverSortie(numero As Integer) As SortieAnalogique
        Return Sorties.FirstOrDefault(Function(s) s.Numero = numero)
    End Function

    Public Function VoiesDebit() As List(Of VoieMesure)
        Return Voies.Where(Function(v) v.Type = VoieMesure.TypeVoie.Debit).ToList()
    End Function

    Public Function VoiesTemperature() As List(Of VoieMesure)
        Return Voies.Where(Function(v) v.Type = VoieMesure.TypeVoie.Temperature).ToList()
    End Function

    Public Function VoiesEnAlarme() As List(Of VoieMesure)
        Return Voies.Where(Function(v) v.EnAlarme).ToList()
    End Function

    Public Function SortiesActives() As List(Of SortieAnalogique)
        Return Sorties.Where(Function(s) s.Active).ToList()
    End Function

    ' ─── CSV ──────────────────────────────────────────────────────────────────

    Public Function EnteteCSV() As String
        Dim cols As New List(Of String) From {"Horodatage"}
        For Each v In Voies.Where(Function(x) x.Active)
            cols.Add(String.Format("{0} ({1})", v.Nom, v.Unite))
        Next
        For Each s In SortiesActives()
            cols.Add(String.Format("{0} (ON/OFF)", s.Nom))
        Next
        Return String.Join(OngletCSV.SEPARATEUR, cols)
    End Function

    Public Function LigneCSV(horodatage As DateTime) As String
        Dim cols As New List(Of String)
        cols.Add(horodatage.ToString("yyyy-MM-dd HH:mm:ss"))
        Dim fmt = "F3"   ' format par défaut — cette méthode est conservée pour compatibilité
        For Each v In Voies.Where(Function(x) x.Active)
            cols.Add(If(Double.IsNaN(v.Valeur) OrElse v.EnErreur,
                "ERREUR",
                v.Valeur.ToString(fmt, System.Globalization.CultureInfo.InvariantCulture)))
        Next
        For Each s In SortiesActives()
            cols.Add(If(s.EstOn, "1", "0"))
        Next
        Return String.Join(OngletCSV.SEPARATEUR, cols)
    End Function

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Public Sub SauverNomsDansConfig(cfg As ConfigManager)
        For Each v In Voies
            v.SauverDansConfig(cfg)
        Next
        For Each s In Sorties
            s.SauverDansConfig(cfg)
        Next
    End Sub

    Public Sub ChargerNomsDepuisConfig(cfg As ConfigManager)
        For Each v In Voies
            v.ChargerDepuisConfig(cfg)
        Next
        For Each s In Sorties
            s.ChargerDepuisConfig(cfg)
        Next
    End Sub

End Class
