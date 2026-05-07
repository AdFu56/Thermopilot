Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  TYPES DE MESURE DISPONIBLES
' ═══════════════════════════════════════════════════════════════════════════════

Public Enum TypeMesure
    TemperatureTC       ' Thermocouple K, J, T, E, N, R, S, B
    Courant4_20mA       ' Générique 4-20 mA avec shunt (débit, pression, humidité...)
    Pression4_20mA      ' Pression via 4-20 mA (alias explicite)
    Humidite4_20mA      ' Humidité via 4-20 mA (alias explicite)
    TensionDC_0_5V      ' Tension DC 0-5 V
    TensionDC_0_10V     ' Tension DC 0-10 V
    TensionDC_0_300V    ' Tension DC 0-300 V via diviseur
    ResistancePT        ' PT100 / PT1000 (résistance → température)
    FrequenceImpulsions ' Débitmètre à impulsions
    PuissanceW          ' Puissance électrique (W)
End Enum

' ═══════════════════════════════════════════════════════════════════════════════
'  PÉRIPHÉRIQUE : un dispositif défini par l'utilisateur
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Décrit un périphérique connecté à la centrale :
''' son nom, son type de mesure et tous ses paramètres de conversion.
''' La liste des périphériques est partagée par toutes les centrales.
''' </summary>
Public Class Peripherique

    Public Property Id          As String = ""    ' identifiant unique généré
    Public Property Nom         As String = ""    ' ex : "Débitmètre entrée"
    Public Property Type        As TypeMesure = TypeMesure.TemperatureTC
    Public Property Description As String = ""   ' note libre

    ' ── Paramètres communs aux capteurs à signal normalisé ──

    ''' <summary>Résistance shunt (Ω) — utilisée pour 4-20 mA.</summary>
    Public Property RShuntOhm   As Double = 250.0

    ''' <summary>Borne basse du signal électrique (mA ou V selon type).</summary>
    Public Property SignalMin   As Double = 4.0

    ''' <summary>Borne haute du signal électrique (mA ou V selon type).</summary>
    Public Property SignalMax   As Double = 20.0

    ''' <summary>Valeur physique minimale correspondant à SignalMin.</summary>
    Public Property ValMin      As Double = 0.0

    ''' <summary>Valeur physique maximale correspondant à SignalMax.</summary>
    Public Property ValMax      As Double = 100.0

    ''' <summary>Unité de la valeur physique (L/h, bar, %, °C, V, W...).</summary>
    Public Property Unite       As String = ""

    ''' <summary>Tension d'alimentation du capteur (V) — informatif uniquement.</summary>
    Public Property AlimCapteurV As Double = 24.0

    ' ── Paramètre spécifique TC ──
    ''' <summary>Type de thermocouple : K, J, T, E, N, R, S, B.</summary>
    Public Property TypeTC      As String = "K"

    ' ── Paramètre spécifique fréquence ──
    ''' <summary>Facteur de conversion impulsions/unité (ex : 450 imp/L).</summary>
    Public Property FacteurFreq As Double = 1.0

    ' ── Alarmes ──
    Public Property AlarmeActive As Boolean = False
    Public Property SeuilBas     As Double  = Double.NaN
    Public Property SeuilHaut    As Double  = Double.NaN
    Public Property Hysteresis   As Double  = 0.5

    ' ─── Libellé affiché dans la liste déroulante ────────────────────────────

    Public ReadOnly Property Libelle As String
        Get
            If Nom.Trim() = "" Then Return TypeLibelle
            Return Nom.Trim() & "  —  " & TypeLibelle
        End Get
    End Property

    Public ReadOnly Property TypeLibelle As String
        Get
            Select Case Type
                Case TypeMesure.TemperatureTC       : Return "Température TC-" & TypeTC
                Case TypeMesure.Courant4_20mA       : Return "4-20 mA"
                Case TypeMesure.Pression4_20mA      : Return "Pression 4-20 mA"
                Case TypeMesure.Humidite4_20mA      : Return "Humidité 4-20 mA"
                Case TypeMesure.TensionDC_0_5V      : Return "Tension DC 0-5 V"
                Case TypeMesure.TensionDC_0_10V     : Return "Tension DC 0-10 V"
                Case TypeMesure.TensionDC_0_300V    : Return "Tension DC 0-300 V"
                Case TypeMesure.ResistancePT        : Return "Résistance PT100/PT1000"
                Case TypeMesure.FrequenceImpulsions : Return "Fréquence (impulsions)"
                Case TypeMesure.PuissanceW          : Return "Puissance (W)"
                Case Else                            : Return "?"
            End Select
        End Get
    End Property

    ''' <summary>
    ''' Retourne True si ce type utilise un signal normalisé via shunt
    ''' (donc nécessite RShunt, SignalMin/Max, ValMin/Max).
    ''' </summary>
    Public ReadOnly Property UtiliseShunt As Boolean
        Get
            Return Type = TypeMesure.Courant4_20mA OrElse
                   Type = TypeMesure.Pression4_20mA OrElse
                   Type = TypeMesure.Humidite4_20mA
        End Get
    End Property

    ''' <summary>
    ''' Retourne True si ce type mesure directement une tension.
    ''' </summary>
    Public ReadOnly Property EstTension As Boolean
        Get
            Return Type = TypeMesure.TensionDC_0_5V  OrElse
                   Type = TypeMesure.TensionDC_0_10V OrElse
                   Type = TypeMesure.TensionDC_0_300V
        End Get
    End Property

    ''' <summary>Convertit la valeur brute Keithley en valeur physique.</summary>
    Public Function Convertir(valeurBrute As Double) As Double
        Select Case Type
            Case TypeMesure.TemperatureTC
                Return valeurBrute   ' Keithley retourne directement °C

            Case TypeMesure.Courant4_20mA, TypeMesure.Pression4_20mA, TypeMesure.Humidite4_20mA
                ' V mesurée → I (mA) → valeur physique
                If RShuntOhm <= 0 Then Return Double.NaN
                Dim iMA   = (valeurBrute / RShuntOhm) * 1000.0
                Dim plage = SignalMax - SignalMin
                If plage <= 0 Then Return Double.NaN
                Dim norm  = Math.Max(0.0, Math.Min(1.0, (iMA - SignalMin) / plage))
                Return ValMin + norm * (ValMax - ValMin)

            Case TypeMesure.TensionDC_0_5V, TypeMesure.TensionDC_0_10V, TypeMesure.TensionDC_0_300V
                ' Interpolation linéaire signal → valeur
                Dim plageT = SignalMax - SignalMin
                If plageT <= 0 Then Return valeurBrute
                Dim normT = Math.Max(0.0, Math.Min(1.0, (valeurBrute - SignalMin) / plageT))
                Return ValMin + normT * (ValMax - ValMin)

            Case TypeMesure.FrequenceImpulsions
                Return If(FacteurFreq > 0, valeurBrute / FacteurFreq, valeurBrute)

            Case TypeMesure.ResistancePT, TypeMesure.PuissanceW
                Return valeurBrute   ' valeur directe

            Case Else
                Return valeurBrute
        End Select
    End Function

    ' ─── Persistance ─────────────────────────────────────────────────────────

    Public ReadOnly Property CleIni As String
        Get
            Return "Periph_" & Id
        End Get
    End Property

    Public Sub SauverDansConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_PERIPH
        cfg.Set_(s, CleIni & "_Nom",      Nom)
        cfg.Set_(s, CleIni & "_Type",     CInt(Type))
        cfg.Set_(s, CleIni & "_Desc",     Description)
        cfg.Set_(s, CleIni & "_RShunt",   RShuntOhm)
        cfg.Set_(s, CleIni & "_SigMin",   SignalMin)
        cfg.Set_(s, CleIni & "_SigMax",   SignalMax)
        cfg.Set_(s, CleIni & "_ValMin",   ValMin)
        cfg.Set_(s, CleIni & "_ValMax",   ValMax)
        cfg.Set_(s, CleIni & "_Unite",    Unite)
        cfg.Set_(s, CleIni & "_AlimV",    AlimCapteurV)
        cfg.Set_(s, CleIni & "_TypeTC",   TypeTC)
        cfg.Set_(s, CleIni & "_Freq",     FacteurFreq)
        cfg.Set_(s, CleIni & "_Alarme",   AlarmeActive)
        cfg.Set_(s, CleIni & "_SBas",     If(Double.IsNaN(SeuilBas),  "", SeuilBas.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        cfg.Set_(s, CleIni & "_SHaut",    If(Double.IsNaN(SeuilHaut), "", SeuilHaut.ToString(System.Globalization.CultureInfo.InvariantCulture)))
        cfg.Set_(s, CleIni & "_Hyst",     Hysteresis)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Dim s = ConfigManager.SEC_PERIPH
        Nom          = cfg.Get_(s,    CleIni & "_Nom",    "")
        Type         = CType(cfg.GetInt(s, CleIni & "_Type", 0), TypeMesure)
        Description  = cfg.Get_(s,    CleIni & "_Desc",   "")
        RShuntOhm    = cfg.GetDouble(s, CleIni & "_RShunt", 250.0)
        SignalMin    = cfg.GetDouble(s, CleIni & "_SigMin", 4.0)
        SignalMax    = cfg.GetDouble(s, CleIni & "_SigMax", 20.0)
        ValMin       = cfg.GetDouble(s, CleIni & "_ValMin", 0.0)
        ValMax       = cfg.GetDouble(s, CleIni & "_ValMax", 100.0)
        Unite        = cfg.Get_(s,    CleIni & "_Unite",  "")
        AlimCapteurV = cfg.GetDouble(s, CleIni & "_AlimV",  24.0)
        TypeTC       = cfg.Get_(s,    CleIni & "_TypeTC", "K")
        FacteurFreq  = cfg.GetDouble(s, CleIni & "_Freq",   1.0)
        AlarmeActive = cfg.GetBool(s, CleIni & "_Alarme",  False)
        Hysteresis   = cfg.GetDouble(s, CleIni & "_Hyst",   0.5)
        Dim sb = cfg.Get_(s, CleIni & "_SBas",  "")
        Dim sh = cfg.Get_(s, CleIni & "_SHaut", "")
        SeuilBas  = If(sb = "", Double.NaN, Double.Parse(sb, System.Globalization.CultureInfo.InvariantCulture))
        SeuilHaut = If(sh = "", Double.NaN, Double.Parse(sh, System.Globalization.CultureInfo.InvariantCulture))
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  BIBLIOTHÈQUE DE PÉRIPHÉRIQUES
' ═══════════════════════════════════════════════════════════════════════════════

Public Class BibliothequePeripheriques

    Public Property Items As New List(Of Peripherique)

    ' ─── Types de mesure disponibles (libellés pour ComboBox) ────────────────

    Public Shared ReadOnly Property TypesDisponibles As String()
        Get
            Return {
                "Température TC (K, J, T, E...)",
                "Courant 4-20 mA (générique)",
                "Pression 4-20 mA",
                "Humidité 4-20 mA",
                "Tension DC 0-5 V",
                "Tension DC 0-10 V",
                "Tension DC 0-300 V (diviseur)",
                "Résistance PT100/PT1000",
                "Fréquence (impulsions)",
                "Puissance électrique (W)"
            }
        End Get
    End Property

    Public Shared Function TypeMesureDepuisLibelle(libelle As String) As TypeMesure
        Select Case libelle
            Case "Température TC (K, J, T, E...)"  : Return TypeMesure.TemperatureTC
            Case "Courant 4-20 mA (générique)"      : Return TypeMesure.Courant4_20mA
            Case "Pression 4-20 mA"                  : Return TypeMesure.Pression4_20mA
            Case "Humidité 4-20 mA"                  : Return TypeMesure.Humidite4_20mA
            Case "Tension DC 0-5 V"                  : Return TypeMesure.TensionDC_0_5V
            Case "Tension DC 0-10 V"                 : Return TypeMesure.TensionDC_0_10V
            Case "Tension DC 0-300 V (diviseur)"     : Return TypeMesure.TensionDC_0_300V
            Case "Résistance PT100/PT1000"            : Return TypeMesure.ResistancePT
            Case "Fréquence (impulsions)"             : Return TypeMesure.FrequenceImpulsions
            Case "Puissance électrique (W)"           : Return TypeMesure.PuissanceW
            Case Else                                 : Return TypeMesure.TemperatureTC
        End Select
    End Function

    ' ─── Accès ────────────────────────────────────────────────────────────────

    Public Function TrouverParId(id As String) As Peripherique
        Return Items.FirstOrDefault(Function(p) p.Id = id)
    End Function

    Public Function TrouverParLibelle(libelle As String) As Peripherique
        Return Items.FirstOrDefault(Function(p) p.Libelle = libelle)
    End Function

    ''' <summary>Liste des libellés pour remplir une ComboBox.</summary>
    Public Function Libelles() As String()
        Return Items.Select(Function(p) p.Libelle).ToArray()
    End Function

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Public Sub SauverDansConfig(cfg As ConfigManager)
        cfg.Set_(ConfigManager.SEC_PERIPH, "NbPeripheriques", Items.Count)
        For i As Integer = 0 To Items.Count - 1
            cfg.Set_(ConfigManager.SEC_PERIPH, "Periph_" & i & "_Id", Items(i).Id)
            Items(i).SauverDansConfig(cfg)
        Next
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Items.Clear()
        Dim nb = cfg.GetInt(ConfigManager.SEC_PERIPH, "NbPeripheriques", 0)
        For i As Integer = 0 To nb - 1
            Dim id = cfg.Get_(ConfigManager.SEC_PERIPH, "Periph_" & i & "_Id", i.ToString())
            Dim p As New Peripherique() With {.Id = id}
            p.ChargerDepuisConfig(cfg)
            Items.Add(p)
        Next
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  ONGLET PÉRIPHÉRIQUES
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Onglet commun "Périphériques" — bibliothèque de tous les dispositifs
''' connectés aux centrales. Placé entre Connexion et Centrale 1.
''' Chaque périphérique définit son nom, son type de mesure et ses paramètres
''' de conversion. La liste résultante alimente la colonne Type des onglets Centrale.
''' </summary>
Public Class OngletPeripheriques

    Public Property Config      As ConfigManager
    Public Property Bibliotheque As BibliothequePeripheriques

    ' ─── Événements ───────────────────────────────────────────────────────────

    Public Event BibliothequeModifiee(sender As Object)
    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Contrôles ────────────────────────────────────────────────────────────

    Private _dgv          As New DataGridView()
    Private _btnAjouter   As New Button()
    Private _btnSupprimer As New Button()
    Private _btnDupliquer As New Button()
    Private _btnSauver    As New Button()

    ' ─── Construction ─────────────────────────────────────────────────────────

    Public Function ConstruirePanel() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' En-tête
        Dim pnlTop As New Panel() With {.Dock = DockStyle.Top, .Height = 54, .Padding = New Padding(8, 6, 8, 4)}

        Dim lblTitre As New Label() With {
            .Text      = "BIBLIOTHÈQUE DES PÉRIPHÉRIQUES",
            .Dock      = DockStyle.Top,
            .Height    = 22,
            .Font      = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 100, 160)
        }
        Dim lblNote As New Label() With {
            .Text      = "Définissez ici les capteurs connectés aux centrales. " &
                         "Les périphériques apparaîtront dans la colonne « Type » de chaque onglet Centrale.",
            .Dock      = DockStyle.Top,
            .Height    = 20,
            .Font      = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.Gray
        }
        pnlTop.Controls.Add(lblNote)
        pnlTop.Controls.Add(lblTitre)

        ' Barre de boutons
        Dim tb As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Top,
            .AutoSize      = True,
            .AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            .Padding       = New Padding(6, 6, 6, 6),
            .FlowDirection = FlowDirection.LeftToRight
        }
        _btnAjouter.Text      = "+ Ajouter"
        _btnAjouter.BackColor = Color.FromArgb(40, 110, 175)
        _btnAjouter.ForeColor = Color.White
        _btnAjouter.FlatStyle = FlatStyle.Flat
        _btnAjouter.Width     = 90
        _btnAjouter.Height    = 28
        _btnAjouter.Margin    = New Padding(0, 0, 4, 0)

        _btnDupliquer.Text      = "⎘ Dupliquer"
        _btnDupliquer.BackColor = Color.FromArgb(70, 100, 140)
        _btnDupliquer.ForeColor = Color.White
        _btnDupliquer.FlatStyle = FlatStyle.Flat
        _btnDupliquer.Width     = 95
        _btnDupliquer.Height    = 28
        _btnDupliquer.Margin    = New Padding(0, 0, 4, 0)

        _btnSupprimer.Text      = "✕ Supprimer"
        _btnSupprimer.BackColor = Color.FromArgb(160, 50, 40)
        _btnSupprimer.ForeColor = Color.White
        _btnSupprimer.FlatStyle = FlatStyle.Flat
        _btnSupprimer.Width     = 95
        _btnSupprimer.Height    = 28
        _btnSupprimer.Margin    = New Padding(0, 0, 16, 0)

        _btnSauver.Text      = "💾 Sauvegarder"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80)
        _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat
        _btnSauver.Width     = 120
        _btnSauver.Height    = 28
        _btnSauver.Margin    = New Padding(0, 0, 0, 0)

        tb.Controls.AddRange({_btnAjouter, _btnDupliquer, _btnSupprimer, _btnSauver})

        ' Grille
        ConstruireGrille()

        pnl.Controls.Add(_dgv)
        pnl.Controls.Add(tb)
        pnl.Controls.Add(pnlTop)

        AddHandler _btnAjouter.Click,   AddressOf BtnAjouter_Click
        AddHandler _btnDupliquer.Click, AddressOf BtnDupliquer_Click
        AddHandler _btnSupprimer.Click, AddressOf BtnSupprimer_Click
        AddHandler _btnSauver.Click,    AddressOf BtnSauver_Click
        AddHandler _dgv.CellValueChanged, AddressOf Dgv_CellValueChanged

        Return pnl
    End Function

    Private Sub ConstruireGrille()
        _dgv.Dock                  = DockStyle.Fill
        _dgv.AllowUserToAddRows    = False
        _dgv.AllowUserToDeleteRows = False
        _dgv.RowHeadersVisible     = False
        _dgv.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgv.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgv.Font                  = New Font("Segoe UI", 9)
        _dgv.BackgroundColor       = Color.White
        _dgv.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgv.GridColor             = Color.FromArgb(210, 215, 230)

        ' Colonnes de base
        Dim colNom As New DataGridViewTextBoxColumn() With {
            .Name = "cNom", .HeaderText = "Nom dispositif", .Width = 180,
            .ToolTipText = "Nom du capteur ou de la mesure (ex : Débitmètre entrée)"
        }
        Dim colType As New DataGridViewComboBoxColumn() With {
            .Name = "cType", .HeaderText = "Type de mesure", .Width = 200
        }
        colType.Items.AddRange(BibliothequePeripheriques.TypesDisponibles)

        Dim colUnite As New DataGridViewTextBoxColumn() With {
            .Name = "cUnite", .HeaderText = "Unité", .Width = 70,
            .ToolTipText = "Unité de la valeur physique (°C, L/h, bar, %, V, W...)"
        }
        Dim colTC As New DataGridViewComboBoxColumn() With {
            .Name = "cTC", .HeaderText = "TC", .Width = 55,
            .ToolTipText = "Type de thermocouple (uniquement pour TC)"
        }
        colTC.Items.AddRange({"K", "J", "T", "E", "N", "R", "S", "B"})

        Dim colRShunt As New DataGridViewTextBoxColumn() With {
            .Name = "cRShunt", .HeaderText = "R shunt (Ω)", .Width = 90,
            .ToolTipText = "Résistance shunt en ohms (pour 4-20 mA, typiquement 250 Ω)"
        }
        Dim colSigMin As New DataGridViewTextBoxColumn() With {
            .Name = "cSigMin", .HeaderText = "Signal min", .Width = 85,
            .ToolTipText = "Valeur minimale du signal électrique (mA ou V)"
        }
        Dim colSigMax As New DataGridViewTextBoxColumn() With {
            .Name = "cSigMax", .HeaderText = "Signal max", .Width = 85,
            .ToolTipText = "Valeur maximale du signal électrique (mA ou V)"
        }
        Dim colValMin As New DataGridViewTextBoxColumn() With {
            .Name = "cValMin", .HeaderText = "Val. min", .Width = 80,
            .ToolTipText = "Valeur physique minimale (correspond à Signal min)"
        }
        Dim colValMax As New DataGridViewTextBoxColumn() With {
            .Name = "cValMax", .HeaderText = "Val. max", .Width = 80,
            .ToolTipText = "Valeur physique maximale (correspond à Signal max)"
        }
        Dim colAlim As New DataGridViewTextBoxColumn() With {
            .Name = "cAlim", .HeaderText = "Alim. capteur (V)", .Width = 110,
            .ToolTipText = "Tension d'alimentation du capteur — informatif uniquement"
        }
        Dim colFreq As New DataGridViewTextBoxColumn() With {
            .Name = "cFreq", .HeaderText = "Facteur (imp/unit.)", .Width = 120,
            .ToolTipText = "Facteur de conversion impulsions → unité (ex : 450 imp/L)"
        }
        Dim colAlarme As New DataGridViewCheckBoxColumn() With {
            .Name = "cAlarme", .HeaderText = "Alarme", .Width = 60
        }
        Dim colSBas As New DataGridViewTextBoxColumn() With {
            .Name = "cSBas", .HeaderText = "Seuil bas", .Width = 80
        }
        Dim colSHaut As New DataGridViewTextBoxColumn() With {
            .Name = "cSHaut", .HeaderText = "Seuil haut", .Width = 80
        }
        Dim colDesc As New DataGridViewTextBoxColumn() With {
            .Name = "cDesc", .HeaderText = "Description / note", .Width = 200
        }

        _dgv.Columns.AddRange({
            colNom, colType, colUnite, colTC,
            colRShunt, colSigMin, colSigMax, colValMin, colValMax,
            colAlim, colFreq,
            colAlarme, colSBas, colSHaut,
            colDesc
        })

        ' Style en-têtes
        _dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255)
        _dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(40, 60, 110)
        _dgv.ColumnHeadersDefaultCellStyle.Font      = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgv.EnableHeadersVisualStyles = False
    End Sub

    ' ─── Alimentation depuis la bibliothèque ─────────────────────────────────

    Public Sub RemplirGrille()
        ' Suspendre l'événement CellValueChanged pendant le remplissage
        ' pour éviter que SyncBibliothequeDepuisGrille modifie Bibliotheque.Items
        ' pendant qu'on l'itère.
        RemoveHandler _dgv.CellValueChanged, AddressOf Dgv_CellValueChanged

        _dgv.Rows.Clear()
        If Bibliotheque IsNot Nothing Then
            ' Copie défensive de la liste pour éviter toute invalidation
            Dim copie = Bibliotheque.Items.ToList()
            For Each p In copie
                AjouterLigne(p)
            Next
        End If

        AddHandler _dgv.CellValueChanged, AddressOf Dgv_CellValueChanged
    End Sub

    Private Sub AjouterLigne(p As Peripherique)
        Dim idx = _dgv.Rows.Add(
            p.Nom,
            LibelleType(p.Type),
            p.Unite,
            p.TypeTC,
            p.RShuntOhm.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.SignalMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.SignalMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.ValMin.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.ValMax.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.AlimCapteurV.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.FacteurFreq.ToString(System.Globalization.CultureInfo.InvariantCulture),
            p.AlarmeActive,
            If(Double.IsNaN(p.SeuilBas),  "", p.SeuilBas.ToString("F2",  System.Globalization.CultureInfo.InvariantCulture)),
            If(Double.IsNaN(p.SeuilHaut), "", p.SeuilHaut.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)),
            p.Description
        )
        _dgv.Rows(idx).Tag = p.Id
        ActualiserStyleLigne(idx)
    End Sub

    ' ─── Style conditionnel selon le type ────────────────────────────────────

    Private Sub ActualiserStyleLigne(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= _dgv.Rows.Count Then Return
        Dim row      = _dgv.Rows(rowIndex)
        Dim typeStr  = If(row.Cells("cType").Value IsNot Nothing, row.Cells("cType").Value.ToString(), "")
        Dim tm       = BibliothequePeripheriques.TypeMesureDepuisLibelle(typeStr)
        Dim estTC    = (tm = TypeMesure.TemperatureTC)
        Dim estShunt = New Peripherique() With {.Type = tm}.UtiliseShunt
        Dim estFreq  = (tm = TypeMesure.FrequenceImpulsions)
        Dim estTens  = New Peripherique() With {.Type = tm}.EstTension

        ' TC : seul TypeTC est pertinent
        GriserCellule(row, "cTC",     Not estTC)
        GriserCellule(row, "cRShunt", Not estShunt)
        GriserCellule(row, "cSigMin", estTC)
        GriserCellule(row, "cSigMax", estTC)
        GriserCellule(row, "cValMin", estTC)
        GriserCellule(row, "cValMax", estTC)
        GriserCellule(row, "cFreq",   Not estFreq)

        ' Colorer la ligne selon la famille
        Dim coulFond As Color
        Select Case True
            Case estTC    : coulFond = Color.FromArgb(255, 248, 235)
            Case estShunt : coulFond = Color.FromArgb(232, 248, 255)
            Case estTens  : coulFond = Color.FromArgb(240, 255, 235)
            Case estFreq  : coulFond = Color.FromArgb(250, 235, 255)
            Case Else     : coulFond = Color.White
        End Select
        row.DefaultCellStyle.BackColor = coulFond
    End Sub

    Private Sub GriserCellule(row As DataGridViewRow, nom As String, griser As Boolean)
        If Not _dgv.Columns.Contains(nom) Then Return
        Dim cell = row.Cells(nom)
        cell.ReadOnly        = griser
        cell.Style.BackColor = If(griser, Color.FromArgb(235, 235, 235), Color.Empty)
        cell.Style.ForeColor = If(griser, Color.FromArgb(180, 180, 180), Color.Empty)
        If griser Then cell.Value = ""
    End Sub

    Private Sub Dgv_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        ActualiserStyleLigne(e.RowIndex)
        ' Notifier les onglets Centrale que la liste a changé
        SyncBibliothequeDepuisGrille()
        RaiseEvent BibliothequeModifiee(Me)
    End Sub

    ' ─── Synchronisation grille ↔ bibliothèque ───────────────────────────────

    Private Sub SyncBibliothequeDepuisGrille()
        If Bibliotheque Is Nothing Then Return
        Bibliotheque.Items.Clear()
        For Each row As DataGridViewRow In _dgv.Rows
            Dim p = LirePeripherique(row)
            If p IsNot Nothing Then Bibliotheque.Items.Add(p)
        Next
    End Sub

    Private Function LirePeripherique(row As DataGridViewRow) As Peripherique
        Dim nom = If(row.Cells("cNom").Value IsNot Nothing, row.Cells("cNom").Value.ToString(), "")
        If nom.Trim() = "" Then Return Nothing
        Dim id = If(row.Tag IsNot Nothing, row.Tag.ToString(), Guid.NewGuid().ToString("N").Substring(0, 8))
        Dim typeStr = If(row.Cells("cType").Value IsNot Nothing, row.Cells("cType").Value.ToString(), "")
        Dim tm = BibliothequePeripheriques.TypeMesureDepuisLibelle(typeStr)
        Dim p As New Peripherique() With {
            .Id          = id,
            .Nom         = nom,
            .Type        = tm,
            .Unite       = CellStr(row, "cUnite"),
            .TypeTC      = If(CellStr(row, "cTC") = "", "K", CellStr(row, "cTC")),
            .RShuntOhm   = ParseD(row.Cells("cRShunt").Value, 250.0),
            .SignalMin    = ParseD(row.Cells("cSigMin").Value, 4.0),
            .SignalMax    = ParseD(row.Cells("cSigMax").Value, 20.0),
            .ValMin       = ParseD(row.Cells("cValMin").Value, 0.0),
            .ValMax       = ParseD(row.Cells("cValMax").Value, 100.0),
            .AlimCapteurV = ParseD(row.Cells("cAlim").Value,  24.0),
            .FacteurFreq  = ParseD(row.Cells("cFreq").Value,  1.0),
            .AlarmeActive = CBool(If(row.Cells("cAlarme").Value, False)),
            .SeuilBas     = ParseD(row.Cells("cSBas").Value,  Double.NaN),
            .SeuilHaut    = ParseD(row.Cells("cSHaut").Value, Double.NaN),
            .Description  = CellStr(row, "cDesc")
        }
        row.Tag = p.Id
        Return p
    End Function

    ' ─── Boutons ─────────────────────────────────────────────────────────────

    Private Sub BtnAjouter_Click(sender As Object, e As EventArgs)
        Dim idx = _dgv.Rows.Add(
            "Nouveau capteur", "Température TC (K, J, T, E...)", "°C", "K",
            "250", "4", "20", "0", "100", "24", "1",
            False, "", "", "")
        _dgv.Rows(idx).Tag = Guid.NewGuid().ToString("N").Substring(0, 8)
        ActualiserStyleLigne(idx)
        _dgv.CurrentCell = _dgv.Rows(idx).Cells("cNom")
        _dgv.BeginEdit(True)
        SyncBibliothequeDepuisGrille()
        RaiseEvent BibliothequeModifiee(Me)
    End Sub

    Private Sub BtnDupliquer_Click(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        Dim src = _dgv.SelectedRows(0)
        Dim vals = (From c As DataGridViewCell In src.Cells Select c.Value).ToArray()
        Dim idx  = _dgv.Rows.Add(vals)
        _dgv.Rows(idx).Tag = Guid.NewGuid().ToString("N").Substring(0, 8)
        ' Modifier le nom pour indiquer la copie
        Dim nomSrc = If(_dgv.Rows(idx).Cells("cNom").Value IsNot Nothing,
                        _dgv.Rows(idx).Cells("cNom").Value.ToString(), "")
        _dgv.Rows(idx).Cells("cNom").Value = nomSrc & " (copie)"
        ActualiserStyleLigne(idx)
        SyncBibliothequeDepuisGrille()
        RaiseEvent BibliothequeModifiee(Me)
    End Sub

    Private Sub BtnSupprimer_Click(sender As Object, e As EventArgs)
        If _dgv.SelectedRows.Count = 0 Then Return
        _dgv.Rows.Remove(_dgv.SelectedRows(0))
        SyncBibliothequeDepuisGrille()
        RaiseEvent BibliothequeModifiee(Me)
    End Sub

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        SyncBibliothequeDepuisGrille()
        Try
            ' 1. Sauvegarder dans peripheriques.ini (fichier global partagé)
            '    On crée un ConfigManager temporaire SANS modifier CheminFichier
            Dim cfgP As New ConfigManager()
            If IO.File.Exists(ConfigManager.CheminPeripheriques) Then
                Dim sauveCheminFichier = ConfigManager.CheminFichier
                cfgP.ChargerDepuis(ConfigManager.CheminPeripheriques)
                ConfigManager.CheminFichier = sauveCheminFichier  ' restaurer
            End If
            If Bibliotheque IsNot Nothing Then Bibliotheque.SauverDansConfig(cfgP)
            Dim sauveCheminFichier2 = ConfigManager.CheminFichier
            cfgP.SauvegarderDans(ConfigManager.CheminPeripheriques)
            ConfigManager.CheminFichier = sauveCheminFichier2  ' restaurer
            ' 2. Aussi dans la config courante pour compatibilité
            If Bibliotheque IsNot Nothing Then Bibliotheque.SauverDansConfig(Config)
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "Bibliothèque sauvegardée (peripheriques.ini).", False)
            RaiseEvent BibliothequeModifiee(Me)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Erreur sauvegarde",
                            MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ─── Chargement ───────────────────────────────────────────────────────────

    Public Sub ChargerDepuisConfig()
        ' Priorité : peripheriques.ini (global) ; sinon config courante
        ' CheminFichier est sauvegardé/restauré pour ne pas être modifié
        Dim cfgSrc As ConfigManager
        If IO.File.Exists(ConfigManager.CheminPeripheriques) Then
            Dim sauve = ConfigManager.CheminFichier
            cfgSrc = New ConfigManager()
            cfgSrc.ChargerDepuis(ConfigManager.CheminPeripheriques)
            ConfigManager.CheminFichier = sauve
        Else
            cfgSrc = Config
        End If
        If Bibliotheque IsNot Nothing Then Bibliotheque.ChargerDepuisConfig(cfgSrc)
        RemplirGrille()
    End Sub

    ' ─── Utilitaires ──────────────────────────────────────────────────────────

    Private Function LibelleType(tm As TypeMesure) As String
        Select Case tm
            Case TypeMesure.TemperatureTC       : Return "Température TC (K, J, T, E...)"
            Case TypeMesure.Courant4_20mA       : Return "Courant 4-20 mA (générique)"
            Case TypeMesure.Pression4_20mA      : Return "Pression 4-20 mA"
            Case TypeMesure.Humidite4_20mA      : Return "Humidité 4-20 mA"
            Case TypeMesure.TensionDC_0_5V      : Return "Tension DC 0-5 V"
            Case TypeMesure.TensionDC_0_10V     : Return "Tension DC 0-10 V"
            Case TypeMesure.TensionDC_0_300V    : Return "Tension DC 0-300 V (diviseur)"
            Case TypeMesure.ResistancePT        : Return "Résistance PT100/PT1000"
            Case TypeMesure.FrequenceImpulsions : Return "Fréquence (impulsions)"
            Case TypeMesure.PuissanceW          : Return "Puissance électrique (W)"
            Case Else                            : Return "Température TC (K, J, T, E...)"
        End Select
    End Function

    Private Function ParseD(v As Object, defaut As Double) As Double
        If v Is Nothing Then Return defaut
        Dim s = v.ToString().Trim().Replace(",", ".")
        Dim d As Double
        If Double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, d) Then Return d
        Return defaut
    End Function

    Private Function CellStr(row As DataGridViewRow, col As String) As String
        Return If(row.Cells(col).Value IsNot Nothing, row.Cells(col).Value.ToString(), "")
    End Function

End Class
