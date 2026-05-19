Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

''' <summary>
''' Gestionnaire des onglets Voies — crée un onglet "Voies : Centrale N" par centrale.
''' Chaque onglet contient la grille de configuration complète des voies et sorties.
''' </summary>
Public Class GestionnaireOngletsVoies

    Public Property Config        As ConfigManager
    Public Property Gestionnaire  As GestionnaireMultiCentrale
    Public Property Bibliotheque  As BibliothequePeripheriques

    ' TabControl cible (celui du FormPrincipal)
    Public Property TabControlPrincipal As TabControl

    ' Index du premier onglet Voies dans le TabControl
    Public Property IndexDepart As Integer = 2   ' après Connexion + Périphériques

    ' Type TC global
    Private _cmbTypeTCGlobal As New ComboBox()

    ' Onglets créés (un par centrale)
    Private _onglets As New List(Of TabPage)

    ' Panneaux de config par centrale (numéro → panneau)
    Private _panneaux As New Dictionary(Of Integer, PanneauVoiesCentrale)

    ' ─── Événements ───────────────────────────────────────────────────────────

    Public Event VoiesAppliquees(sender As Object, centrale As CentraleKeithley)
    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Reconstruction des onglets ──────────────────────────────────────────

    Public Sub ReconstruireOnglets(nbCentrales As Integer)
        ' Supprimer les anciens onglets Voies
        For Each tabPage As TabPage In _onglets
            TabControlPrincipal.TabPages.Remove(tabPage)
        Next
        _onglets.Clear()
        _panneaux.Clear()

        ' Créer un onglet par centrale
        For i As Integer = 1 To nbCentrales
            Dim c      = Gestionnaire.ObtenirCentrale(i)
            Dim nomTab = If(c IsNot Nothing, "🌡 " & c.NomAffiche, "🌡 Centrale " & i.ToString())

            Dim tabPage As New TabPage(nomTab)
            Dim panneau As New PanneauVoiesCentrale() With {
                .NumeroCentrale = i,
                .Config         = Config,
                .Gestionnaire   = Gestionnaire,
                .Bibliotheque   = Bibliotheque
            }
            AddHandler panneau.VoiesAppliquees, Sub(s, centrale) RaiseEvent VoiesAppliquees(Me, centrale)
            AddHandler panneau.StatutChange,    Sub(s, msg, err) RaiseEvent StatutChange(Me, msg, err)

            tabPage.Controls.Add(panneau.ConstruirePanel(_cmbTypeTCGlobal))
            ' Mettre à jour la liste des périphériques avant de charger la config
            panneau.MettreAJourListePeripheriques(Bibliotheque)
            panneau.ChargerDepuisConfig()

            ' Insérer à la bonne position
            TabControlPrincipal.TabPages.Insert(IndexDepart + i - 1, tabPage)
            _onglets.Add(tabPage)
            _panneaux(i) = panneau
        Next
    End Sub

    ''' <summary>
    ''' Propage la configuration de la grille vers le Gestionnaire pour toutes les centrales
    ''' SANS envoyer de commandes SCPI. Appeler au démarrage.
    ''' </summary>
    Public Sub PropagerTousVersGestionnaire()
        For Each kvp In _panneaux
            kvp.Value.PropagerVersGestionnaire()
        Next
    End Sub

    ''' <summary>
    ''' Propage une bibliothèque mise à jour vers tous les panneaux existants.
    ''' Appelé quand l'utilisateur modifie l'onglet Périphériques.
    ''' </summary>
    Public Sub PropagerBibliotheque(biblio As BibliothequePeripheriques)
        Bibliotheque = biblio
        For Each kvp In _panneaux
            kvp.Value.Bibliotheque = biblio
            kvp.Value.MettreAJourListePeripheriques(biblio)
        Next
    End Sub

    ''' <summary>Met à jour le nom de l'onglet quand la centrale est renommée.</summary>
    Public Sub MettreAJourNomOnglet(numeroCentrale As Integer)
        If numeroCentrale < 1 OrElse numeroCentrale > _onglets.Count Then Return
        Dim c = Gestionnaire.ObtenirCentrale(numeroCentrale)
        If c Is Nothing Then Return
        _onglets(numeroCentrale - 1).Text = "🌡 " & c.NomAffiche
    End Sub

    Public Function ObtenirPanneau(numero As Integer) As PanneauVoiesCentrale
        If _panneaux.ContainsKey(numero) Then Return _panneaux(numero)
        Return Nothing
    End Function

    Public ReadOnly Property TypeTC As String
        Get
            Return If(_cmbTypeTCGlobal.SelectedItem IsNot Nothing,
                      _cmbTypeTCGlobal.SelectedItem.ToString(), "K")
        End Get
    End Property

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  PANNEAU DE CONFIGURATION D'UNE CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

Public Class PanneauVoiesCentrale

    Public Property NumeroCentrale As Integer
    Public Property Config         As ConfigManager
    Public Property Gestionnaire   As GestionnaireMultiCentrale
    Public Property Bibliotheque   As BibliothequePeripheriques

    ' Configuration des cartes (une par carte, max 2)
    Private _configCartes As New List(Of ConfigCarte)()
    ' Contrôles dynamiques par carte pour les plages
    Private Class ControlsCarte
        Public CmbType        As New ComboBox()
        Public TxtEntrees     As New TextBox()
        Public TxtSorties     As New TextBox()
        Public LblErrEntrees  As New Label()
        Public LblErrSorties  As New Label()
    End Class
    Private _ctrlCartes As New Dictionary(Of Integer, ControlsCarte)()
    Private _pnlCartes  As New FlowLayoutPanel() With {
        .FlowDirection = FlowDirection.TopDown,
        .WrapContents  = False
    }   ' panneau accueillant les blocs de config cartes

    Private _dgvVoies     As New DataGridView()
    Private _dgvSorties   As New DataGridView()
    Private _numNbCartes  As New NumericUpDown()
    Private _btnAppliquer    As New Button()
    Private _btnSauver       As New Button()
    Private _btnValeursBrutes As New Button()
    Private _refTypeTC    As ComboBox
    Private _chargement   As Boolean = False

    Public Event VoiesAppliquees(sender As Object, centrale As CentraleKeithley)
    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)

    ' ─── Construction ─────────────────────────────────────────────────────────

    Public Function ConstruirePanel(cmbTypeTCGlobal As ComboBox) As Control
        _refTypeTC = cmbTypeTCGlobal

        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' ── Barre supérieure ──
        Dim pnlTop As New FlowLayoutPanel() With {
            .Dock          = DockStyle.Top,
            .AutoSize      = True,
            .AutoSizeMode  = AutoSizeMode.GrowAndShrink,
            .Padding       = New Padding(8, 6, 8, 6),
            .FlowDirection = FlowDirection.LeftToRight,
            .WrapContents  = False
        }

        pnlTop.Controls.Add(New Label() With {
            .Text      = "Nb cartes :", .AutoSize = True,
            .Font      = New Font("Segoe UI", 9, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 90, 140),
            .Margin    = New Padding(0, 7, 4, 0)
        })

        _numNbCartes.Minimum = 1
        _numNbCartes.Maximum = 2
        _numNbCartes.Value   = 1
        _numNbCartes.Width   = 55
        _numNbCartes.Height  = 28
        _numNbCartes.Font    = New Font("Consolas", 10, FontStyle.Bold)
        _numNbCartes.Margin  = New Padding(0, 1, 0, 0)
        pnlTop.Controls.Add(_numNbCartes)

        ' Type TC supprimé — défini dans l'onglet Périphériques pour chaque capteur

        _btnAppliquer.Text      = "⚠ APPLIQUER"
        _btnAppliquer.BackColor = Color.FromArgb(180, 30, 30)
        _btnAppliquer.ForeColor = Color.Yellow
        _btnAppliquer.FlatStyle = FlatStyle.Flat
        _btnAppliquer.Width     = 120
        _btnAppliquer.Height    = 30
        _btnAppliquer.Margin    = New Padding(10, 0, 0, 0)
        _btnAppliquer.Font      = New Font("Segoe UI", 9, FontStyle.Bold)
        pnlTop.Controls.Add(_btnAppliquer)

        ' Message d'aide à côté du bouton
        pnlTop.Controls.Add(New Label() With {
            .Text      = "← Valide et propage la configuration SCPI (obligatoire avant acquisition)",
            .AutoSize  = True,
            .Font      = New Font("Segoe UI", 8, FontStyle.Italic),
            .ForeColor = Color.FromArgb(180, 30, 30),
            .Margin    = New Padding(6, 8, 0, 0)
        })

        _btnSauver.Text      = "💾 Sauvegarder"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80)
        _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat
        _btnSauver.Width     = 120
        _btnSauver.Height    = 30
        _btnSauver.Margin    = New Padding(4, 0, 0, 0)
        pnlTop.Controls.Add(_btnSauver)

        _btnValeursBrutes.Text      = "🔬 Valeurs brutes"
        _btnValeursBrutes.BackColor = Color.FromArgb(50, 70, 100)
        _btnValeursBrutes.ForeColor = Color.White
        _btnValeursBrutes.FlatStyle = FlatStyle.Flat
        _btnValeursBrutes.Width     = 135
        _btnValeursBrutes.Height    = 30
        _btnValeursBrutes.Margin    = New Padding(16, 0, 0, 0)
        Dim ttBrutes As New ToolTip()
        ttBrutes.SetToolTip(_btnValeursBrutes,
            "Affiche les valeurs brutes lues par le Keithley (sans conversion) pour vérifier le câblage")
        pnlTop.Controls.Add(_btnValeursBrutes)

        ' ── Panneau de configuration des cartes ──
        _pnlCartes.Dock         = DockStyle.Top
        _pnlCartes.AutoSize     = True
        _pnlCartes.AutoSizeMode = AutoSizeMode.GrowAndShrink
        _pnlCartes.BackColor    = Color.FromArgb(240, 243, 250)
        _pnlCartes.Padding      = New Padding(8, 4, 8, 4)

        ' ── Split grille voies / grille sorties ──
        Dim split As New SplitContainer()
        split.Dock        = DockStyle.Fill
        split.Orientation = Orientation.Horizontal

        split.Panel1.Controls.Add(ConstruireGrilleVoies())
        split.Panel2.Controls.Add(ConstruireGrilleSorties())

        pnl.Controls.Add(split)
        pnl.Controls.Add(_pnlCartes)
        pnl.Controls.Add(pnlTop)

        AddHandler _btnAppliquer.Click,       AddressOf BtnAppliquer_Click
        AddHandler _btnSauver.Click,          AddressOf BtnSauver_Click
        AddHandler _numNbCartes.ValueChanged, AddressOf NbCartes_Changed
        AddHandler _btnValeursBrutes.Click,   AddressOf BtnValeursBrutes_Click

        ' Construire les blocs cartes initiaux
        RebuildPanneauCartes()

        Return pnl
    End Function

    ' ─── Panneau de configuration des cartes ─────────────────────────────────

    Private Sub RebuildPanneauCartes()
        _pnlCartes.Controls.Clear()
        _ctrlCartes.Clear()

        Dim nb = CInt(_numNbCartes.Value)
        Do While _configCartes.Count < nb
            _configCartes.Add(New ConfigCarte(_configCartes.Count + 1))
        Loop

        ' Parcours en ordre normal — FlowLayout TopDown empile dans l'ordre d'ajout
        For carte As Integer = 1 To nb
            Dim cfg = _configCartes(carte - 1)
            Dim cc  As New ControlsCarte()

            cc.CmbType.Items.AddRange({"Module 7706 (sorties ±Us V)", "Module 7700 (mesure seule)", "Autre"})
            Dim idxType = 0
            If cfg.Type = TypeCarte.Module7700 Then idxType = 1
            If cfg.Type = TypeCarte.Autre Then idxType = 2
            cc.CmbType.SelectedIndex = idxType
            cc.CmbType.DropDownStyle = ComboBoxStyle.DropDownList
            cc.CmbType.Width         = 130
            cc.CmbType.Font          = New Font("Segoe UI", 9)
            cc.CmbType.Margin        = New Padding(0, 1, 0, 0)

            cc.TxtEntrees.Text  = cfg.PlageEntrees.TexteOriginal
            cc.TxtEntrees.Width = 200
            cc.TxtEntrees.Font  = New Font("Consolas", 9)
            cc.TxtEntrees.Margin = New Padding(0, 1, 0, 0)

            cc.TxtSorties.Text  = cfg.PlageSorties.TexteOriginal
            cc.TxtSorties.Width = 130
            cc.TxtSorties.Font  = New Font("Consolas", 9)
            cc.TxtSorties.Margin = New Padding(0, 1, 0, 0)
            ' Masquer la zone Sorties si la carte n'en a pas (Module7700)
            cc.TxtSorties.Visible = (cfg.Type <> TypeCarte.Module7700)
            AddHandler cc.CmbType.SelectedIndexChanged, Sub(s, e)
                ' 0=7706(sorties), 1=7700(pas de sorties), 2=Autre(sorties)
                Dim aSorties = (cc.CmbType.SelectedIndex <> 1)
                cc.TxtSorties.Visible = aSorties
                If Not aSorties Then cc.TxtSorties.Text = ""
            End Sub

            cc.LblErrEntrees.AutoSize  = True
            cc.LblErrEntrees.ForeColor = Color.Red
            cc.LblErrEntrees.Font      = New Font("Segoe UI", 7.5, FontStyle.Italic)
            cc.LblErrEntrees.Visible   = False

            cc.LblErrSorties.AutoSize  = True
            cc.LblErrSorties.ForeColor = Color.Red
            cc.LblErrSorties.Font      = New Font("Segoe UI", 7.5, FontStyle.Italic)
            cc.LblErrSorties.Visible   = False

            Dim tt As New ToolTip()
            Dim lblEntrees As New Label() With {
                .Text     = "  Entrées :",
                .AutoSize = True,
                .Margin   = New Padding(10, 6, 4, 0)
            }
            tt.SetToolTip(lblEntrees,    "Format : 101,103-106,108   (numéros séparés par virgules, plages avec tiret)")
            tt.SetToolTip(cc.TxtEntrees, "Format : 101,103-106,108   (numéros séparés par virgules, plages avec tiret)")

            Dim fl As New FlowLayoutPanel() With {
                .AutoSize      = True,
                .AutoSizeMode  = AutoSizeMode.GrowAndShrink,
                .FlowDirection = FlowDirection.LeftToRight,
                .WrapContents  = False,
                .Padding       = New Padding(4, 3, 4, 3),
                .Margin        = New Padding(0, 0, 0, 2),
                .BackColor     = If(carte = 1,
                    Color.FromArgb(245, 248, 255),
                    Color.FromArgb(245, 255, 248))
            }
            fl.Controls.AddRange({
                New Label() With {
                    .Text      = "Carte " & carte & " —",
                    .AutoSize  = True,
                    .Font      = New Font("Segoe UI", 9, FontStyle.Bold),
                    .ForeColor = Color.FromArgb(60, 80, 140),
                    .Margin    = New Padding(0, 6, 6, 0)},
                New Label() With {.Text = "Type :", .AutoSize = True, .Margin = New Padding(0, 6, 4, 0)},
                cc.CmbType,
                lblEntrees,
                cc.TxtEntrees,
                cc.LblErrEntrees,
                New Label() With {
                    .Text     = "  Sorties (+/-12V) :",
                    .AutoSize = True,
                    .Margin   = New Padding(10, 6, 4, 0)},
                cc.TxtSorties,
                cc.LblErrSorties
            })
            _pnlCartes.Controls.Add(fl)
            _ctrlCartes(carte) = cc
        Next
    End Sub

    ''' <summary>Valide les plages saisies et retourne True si tout est correct.</summary>
    Private Function ValiderPlages() As Boolean
        Dim ok = True
        For carte As Integer = 1 To _ctrlCartes.Count
            Dim cc = _ctrlCartes(carte)

            ' Champ vide = autorisé (pas de voies générées pour cette carte)
            Dim texteE = cc.TxtEntrees.Text.Trim()
            If texteE <> "" Then
                Dim pE As New PlageVoies() With {.TexteOriginal = texteE}
                If Not pE.EstValide Then
                    cc.LblErrEntrees.Text    = "⚠ " & pE.MessageErreur
                    cc.LblErrEntrees.Visible = True
                    ok = False
                Else
                    cc.LblErrEntrees.Visible = False
                End If
            Else
                cc.LblErrEntrees.Visible = False
            End If

            Dim texteS = cc.TxtSorties.Text.Trim()
            If texteS <> "" Then
                Dim pS As New PlageVoies() With {.TexteOriginal = texteS}
                If Not pS.EstValide Then
                    cc.LblErrSorties.Text    = "⚠ " & pS.MessageErreur
                    cc.LblErrSorties.Visible = True
                    ok = False
                Else
                    cc.LblErrSorties.Visible = False
                End If
            Else
                cc.LblErrSorties.Visible = False
            End If
        Next
        Return ok
    End Function

    ' ─── Grille des voies ─────────────────────────────────────────────────────

    Private Function ConstruireGrilleVoies() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}
        Dim lbl As New Label() With {
            .Text      = "VOIES DE MESURE  —  Les paramètres de conversion sont définis dans l'onglet Périphériques",
            .Dock      = DockStyle.Top, .Height = 22,
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(80, 130, 190),
            .Padding   = New Padding(6, 4, 0, 0)
        }

        _dgvVoies.Dock                  = DockStyle.Fill
        _dgvVoies.AllowUserToAddRows    = False
        _dgvVoies.AllowUserToDeleteRows = False
        _dgvVoies.RowHeadersVisible     = False
        _dgvVoies.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvVoies.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgvVoies.Font                  = New Font("Segoe UI", 8.5)
        _dgvVoies.BackgroundColor       = Color.White
        _dgvVoies.SelectionMode         = DataGridViewSelectionMode.FullRowSelect

        Dim colType As New DataGridViewComboBoxColumn() With {
            .Name        = "cType",
            .HeaderText  = "Périphérique connecté",
            .Width       = 230,
            .ToolTipText = "Sélectionner un périphérique défini dans l'onglet Périphériques"
        }

        Dim colSBas As New DataGridViewTextBoxColumn() With {
            .Name        = "cSBas",
            .HeaderText  = "Seuil bas",
            .Width       = 80,
            .ToolTipText = "Borne basse de fonctionnement normal (débit, température, pression...)." & vbCrLf &
                           "Si la valeur descend sous ce seuil ET que 'Surveill. sécu.' est coché," & vbCrLf &
                           "les sorties marquées 'ARRET si Surveill. sécu.' sont forcées à 0V." & vbCrLf &
                           "Laisser vide = pas de contrôle par le bas."
        }
        Dim colSHaut As New DataGridViewTextBoxColumn() With {
            .Name        = "cSHaut",
            .HeaderText  = "Seuil haut",
            .Width       = 80,
            .ToolTipText = "Borne haute de fonctionnement normal (débit, température, pression...)." & vbCrLf &
                           "Si la valeur dépasse ce seuil ET que 'Surveill. sécu.' est coché," & vbCrLf &
                           "les sorties marquées 'ARRET si Surveill. sécu.' sont forcées à 0V." & vbCrLf &
                           "Laisser vide = pas de contrôle par le haut."
        }
        Dim colSurvDebit As New DataGridViewCheckBoxColumn() With {
            .Name        = "cSurvDebit",
            .HeaderText  = "Surveill. sécu.",
            .Width       = 90,
            .ToolTipText = "Cocher pour que cette voie serve de mesure de sécurité." & vbCrLf &
                           "Peut être un débit, une température, une pression..." & vbCrLf &
                           "Si la valeur sort de [Seuil bas, Seuil haut]," & vbCrLf &
                           "toutes les sorties cochées 'ARRET si Surveill. sécu.' dans la grille des sorties" & vbCrLf &
                           "sont forcées à 0V automatiquement." & vbCrLf &
                           "Plusieurs voies peuvent être surveillées simultanément." & vbCrLf &
                           "Si Seuil bas et Seuil haut sont vides → pas de contrôle."
        }

        Dim cols As DataGridViewColumn() = {
            New DataGridViewTextBoxColumn()  With {.Name = "cNum",    .HeaderText = "N° voie",  .Width = 65,  .ReadOnly = True},
            New DataGridViewTextBoxColumn()  With {.Name = "cCarte",  .HeaderText = "Carte",    .Width = 50,  .ReadOnly = True},
            New DataGridViewCheckBoxColumn() With {.Name = "cActif",  .HeaderText = "Actif",    .Width = 50},
            New DataGridViewTextBoxColumn()  With {.Name = "cNom",    .HeaderText = "Nom voie", .Width = 140,
                .ToolTipText = "Nom libre pour identifier cette voie dans les graphiques et le CSV"},
            colType,
            New DataGridViewCheckBoxColumn() With {
                .Name        = "cAlarme",
                .HeaderText  = "Alarme",
                .Width       = 60,
                .ToolTipText = "Cocher pour activer la surveillance d'alarme sur cette voie." & vbCrLf &
                               "Si la valeur dépasse Seuil haut → alarme haute (EnAlarmeHaute)." & vbCrLf &
                               "Si la valeur descend sous Seuil bas → alarme basse (EnAlarmeBasse)." & vbCrLf &
                               "Effet : clignotement visuel dans l'onglet Acquisition + message de statut." & vbCrLf &
                               "Une hystérésis est appliquée pour éviter les oscillations." & vbCrLf &
                               "Peut être combiné avec 'Surveill. sécu.' pour aussi couper les sorties" & vbCrLf &
                               "marquées 'ARRET si Surveill. sécu.' dans la grille des sorties."
            },
            colSBas,
            colSHaut,
            colSurvDebit
        }
        _dgvVoies.Columns.AddRange(cols)

        ' Style en-têtes
        _dgvVoies.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255)
        _dgvVoies.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgvVoies.EnableHeadersVisualStyles = False

        ' Coloration selon le périphérique sélectionné
        AddHandler _dgvVoies.CellValueChanged, Sub(s, e) ActualiserCouleurLigneVoie(e.RowIndex)
        AddHandler _dgvVoies.DataError, Sub(s, ev) ev.ThrowException = False

        pnl.Controls.Add(_dgvVoies)
        pnl.Controls.Add(lbl)
        Return pnl
    End Function

    ''' <summary>Met à jour la liste déroulante Type avec les périphériques disponibles.</summary>
    Public Sub MettreAJourListePeripheriques(biblio As BibliothequePeripheriques)
        If Not _dgvVoies.Columns.Contains("cType") Then Return
        Dim col = TryCast(_dgvVoies.Columns("cType"), DataGridViewComboBoxColumn)
        If col Is Nothing Then Return

        ' Mémoriser les sélections actuelles
        Dim selections As New Dictionary(Of Integer, String)
        For Each row As DataGridViewRow In _dgvVoies.Rows
            If row.Cells("cType").Value IsNot Nothing Then
                Dim num As Integer
                If Integer.TryParse(
                    If(row.Cells("cNum").Value IsNot Nothing, row.Cells("cNum").Value.ToString(), ""), num) Then
                    selections(num) = row.Cells("cType").Value.ToString()
                End If
            End If
        Next

        col.Items.Clear()
        col.Items.Add("— Non connecté —")
        If biblio IsNot Nothing Then
            For Each libelle In biblio.Libelles()
                col.Items.Add(libelle)
            Next
        End If

        ' Restaurer les sélections (si le périphérique existe toujours)
        For Each row As DataGridViewRow In _dgvVoies.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cNum").Value IsNot Nothing, row.Cells("cNum").Value.ToString(), ""), num) Then Continue For
            If selections.ContainsKey(num) Then
                Dim sel = selections(num)
                If col.Items.Contains(sel) Then
                    row.Cells("cType").Value = sel
                Else
                    row.Cells("cType").Value = "— Non connecté —"
                End If
            End If
        Next
    End Sub

    Private Sub ActualiserCouleurLigneVoie(rowIndex As Integer)
        If rowIndex < 0 OrElse rowIndex >= _dgvVoies.Rows.Count Then Return
        Dim row     = _dgvVoies.Rows(rowIndex)
        Dim typeVal = If(row.Cells("cType").Value IsNot Nothing, row.Cells("cType").Value.ToString(), "")
        Dim actif   = CBool(If(row.Cells("cActif").Value, False))

        If Not actif OrElse typeVal = "— Non connecté —" OrElse typeVal = "" Then
            row.DefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248)
            row.DefaultCellStyle.ForeColor = Color.Gray
        Else
            row.DefaultCellStyle.BackColor = Color.White
            row.DefaultCellStyle.ForeColor = Color.Black
        End If
    End Sub

    ' ─── Grille des sorties ───────────────────────────────────────────────────

    Private Function ConstruireGrilleSorties() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}
        Dim lbl As New Label() With {
            .Text      = "SORTIES ANALOGIQUES",
            .Dock      = DockStyle.Top, .Height = 22,
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(160, 80, 30),
            .Padding   = New Padding(6, 4, 0, 0)
        }

        _dgvSorties.Dock                  = DockStyle.Fill
        _dgvSorties.AllowUserToAddRows    = False
        _dgvSorties.AllowUserToDeleteRows = False
        _dgvSorties.RowHeadersVisible     = False
        _dgvSorties.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvSorties.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgvSorties.Font                  = New Font("Segoe UI", 8.5)
        _dgvSorties.BackgroundColor       = Color.FromArgb(255, 252, 245)

        ' Colonne Mode
        Dim colMode As New DataGridViewComboBoxColumn() With {
            .Name = "cSMode", .HeaderText = "Mode", .Width = 110
        }
        colMode.Items.AddRange({"Booléen (0/+Amp)", "Analogique (0..+Amp)", "Analogique full (−Amp..+Amp)"})

        _dgvSorties.Columns.AddRange({
            New DataGridViewTextBoxColumn()  With {.Name = "cSNum",     .HeaderText = "N° sortie",        .Width = 75,  .ReadOnly = True},
            New DataGridViewTextBoxColumn()  With {.Name = "cSCarte",   .HeaderText = "Carte",             .Width = 50,  .ReadOnly = True},
            New DataGridViewCheckBoxColumn() With {.Name = "cSActif",   .HeaderText = "Actif",             .Width = 50},
            New DataGridViewTextBoxColumn()  With {.Name = "cSNom",     .HeaderText = "Nom du dispositif", .Width = 160},
            colMode,
            New DataGridViewTextBoxColumn()  With {.Name = "cSUMax",    .HeaderText = "Amplitude (V)",          .Width = 70},
            New DataGridViewTextBoxColumn()  With {.Name = "cSSeuil",   .HeaderText = "Seuil ON (V)",      .Width = 80},
            New DataGridViewCheckBoxColumn() With {.Name = "cSSecuDebit", .HeaderText = "ARRET si Surveill. sécu.",  .Width = 85,
                .ToolTipText = "Si coché : cette sortie est forcée à 0V quand une voie marquée 'Surveill. sécu.'" & vbCrLf &
                               "sort de sa plage [Seuil bas, Seuil haut]." & vbCrLf &
                               "Fonctionne avec n'importe quelle grandeur surveillée (débit, température, pression...)."}
        })

        ' Tooltip d'aide sur les colonnes
        _dgvSorties.Columns("cSMode").ToolTipText = "Booléen (0/+Amp) : 0 V (OFF) ou +Amplitude V (ON) — tout-ou-rien." & vbCrLf &
                                                    "Analogique (0..+Amp) : tension de 0 V à +Amplitude V." & vbCrLf &
                                                    "Analogique full (−Amp..+Amp) : tension variable de −Amplitude V à +Amplitude V." & vbCrLf &
                                                    "Usage : V3V, actionneur bidirectionnel, tout périphérique commandé en tension symétrique."
        _dgvSorties.Columns("cSUMax").ToolTipText = "Amplitude de tension (V)." & vbCrLf &
                                                    "Booléen : tension appliquée quand ON." & vbCrLf &
                                                    "Analogique : tension maximale (de 0 à +Amplitude)." & vbCrLf &
                                                    "Analogique full : amplitude maximale en valeur absolue (de −Amplitude à +Amplitude)." & vbCrLf &
                                                    "Plage physique module 7706 : ±12 V."
        _dgvSorties.Columns("cSSeuil").ToolTipText   = "Tension à partir de laquelle la sortie est considérée ON (graphique)"

        AddHandler _dgvSorties.CellValueChanged, AddressOf DgvSorties_CellValueChanged
        ' Gérer les erreurs de valeur ComboBox (ex: ancienne valeur de config.ini non reconnue)
        AddHandler _dgvSorties.DataError, Sub(s, ev)
            ' Silencieux — la cellule gardera sa valeur ou sera remise à la valeur par défaut
            ev.ThrowException = False
        End Sub

        pnl.Controls.Add(_dgvSorties)
        pnl.Controls.Add(lbl)
        Return pnl
    End Function

    Private Sub DgvSorties_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        ' Griser le champ Seuil ON si mode Analogique (non pertinent)
        If e.RowIndex < 0 OrElse Not _dgvSorties.Columns.Contains("cSMode") Then Return
        Dim row     = _dgvSorties.Rows(e.RowIndex)
        Dim modeVal = If(row.Cells("cSMode").Value IsNot Nothing, row.Cells("cSMode").Value.ToString(), "")
        Dim estAnal = modeVal.Contains("Analogique") OrElse modeVal.Contains("Analogique full")
        row.Cells("cSSeuil").ReadOnly        = estAnal
        row.Cells("cSSeuil").Style.BackColor = If(estAnal, Color.FromArgb(235, 235, 235), Color.White)
        row.Cells("cSSeuil").Style.ForeColor = If(estAnal, Color.Gray, Color.Black)
    End Sub

    ' ─── Remplissage des grilles ──────────────────────────────────────────────

    Private Sub RemplirGrilles()
        _dgvVoies.Rows.Clear()
        _dgvSorties.Rows.Clear()

        Dim nb = CInt(_numNbCartes.Value)
        For carte As Integer = 1 To nb
            Dim coulFond = If(carte = 1, Color.FromArgb(245, 248, 255), Color.FromArgb(245, 255, 248))

            ' ── Voies d'entrée ──
            Dim texteEntrees = If(_ctrlCartes.ContainsKey(carte),
                _ctrlCartes(carte).TxtEntrees.Text.Trim(), "")

            Dim numsEntrees As List(Of Integer)
            If texteEntrees = "" Then
                ' Champ vide → aucune ligne d'entrée
                numsEntrees = New List(Of Integer)
            Else
                Dim pE As New PlageVoies() With {.TexteOriginal = texteEntrees}
                numsEntrees = If(pE.EstValide, pE.Numeros, New List(Of Integer))
            End If

            For Each num In numsEntrees
                Dim idx = _dgvVoies.Rows.Add(num, "C" & carte, False,
                    "T_" & num.ToString(), "— Non connecté —",
                    False, "", "", False)
                _dgvVoies.Rows(idx).Cells("cNum").Style.BackColor = coulFond
                ActualiserCouleurLigneVoie(idx)
            Next

            ' ── Sorties analogiques ──
            Dim texteSorties = If(_ctrlCartes.ContainsKey(carte),
                _ctrlCartes(carte).TxtSorties.Text.Trim(), "")

            Dim numsSorties As List(Of Integer)
            If texteSorties = "" Then
                ' Champ vide → aucune sortie
                numsSorties = New List(Of Integer)
            Else
                Dim pS As New PlageVoies() With {.TexteOriginal = texteSorties}
                numsSorties = If(pS.EstValide, pS.Numeros, New List(Of Integer))
            End If

            For Each num In numsSorties
                _dgvSorties.Rows.Add(num, "C" & carte, False,
                    "Sortie_" & num.ToString(),
                    "Booléen (0/Umax)", "5", "2.5", False)
            Next
        Next
    End Sub

    Private Sub NbCartes_Changed(sender As Object, e As EventArgs)
        If _chargement Then Return
        RebuildPanneauCartes()
        RemplirGrilles()
        MettreAJourListePeripheriques(Bibliotheque)
    End Sub

    ' ─── Appliquer → GestionVoies de la centrale ──────────────────────────────

    Private Sub BtnAppliquer_Click(sender As Object, e As EventArgs)
        ' Valider les plages avant de construire la grille
        If Not ValiderPlages() Then
            RaiseEvent StatutChange(Me,
                "[C" & NumeroCentrale & "] Plages de voies invalides — corrigez les erreurs en rouge.",
                True)
            Return
        End If

        ' Mettre à jour les configCartes depuis l'IHM
        Dim plagesChangees = False
        For carte As Integer = 1 To _ctrlCartes.Count
            Dim cc = _ctrlCartes(carte)
            Do While _configCartes.Count < carte
                _configCartes.Add(New ConfigCarte(carte))
            Loop
            Dim ancienEntrees = _configCartes(carte - 1).PlageEntrees.TexteOriginal
            Dim ancienSorties = _configCartes(carte - 1).PlageSorties.TexteOriginal
            Dim nouveauEntrees = cc.TxtEntrees.Text.Trim()
            Dim nouveauSorties = cc.TxtSorties.Text.Trim()

            Dim typeCarteChoisi As TypeCarte
            Select Case cc.CmbType.SelectedIndex
                Case 0 : typeCarteChoisi = TypeCarte.Module7706
                Case 1 : typeCarteChoisi = TypeCarte.Module7700
                Case Else : typeCarteChoisi = TypeCarte.Autre
            End Select
            _configCartes(carte - 1).Type = typeCarteChoisi
            _configCartes(carte - 1).PlageEntrees.TexteOriginal = nouveauEntrees
            _configCartes(carte - 1).PlageSorties.TexteOriginal = nouveauSorties

            If ancienEntrees <> nouveauEntrees OrElse ancienSorties <> nouveauSorties Then
                plagesChangees = True
            End If
        Next

        ' Ne régénérer la grille que si les plages ont réellement changé
        ' (évite de perdre les valeurs saisies par l'utilisateur)
        If plagesChangees Then
            RemplirGrilles()
            MettreAJourListePeripheriques(Bibliotheque)
        End If

        Dim c = Gestionnaire.ObtenirCentrale(NumeroCentrale)
        If c Is Nothing Then Return

        c.Voies.Voies.Clear()
        c.Voies.Sorties.Clear()

        Dim voiesTemp  As New List(Of String)
        Dim voiesDebit As New List(Of String)
        Dim typeTC = "K"

        ' Voies
        For Each row As DataGridViewRow In _dgvVoies.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cNum").Value IsNot Nothing, row.Cells("cNum").Value.ToString(), ""),
                num) Then Continue For

            Dim actif = CBool(If(row.Cells("cActif").Value, False))
            If Not actif Then Continue For

            Dim nom      = If(row.Cells("cNom").Value IsNot Nothing, row.Cells("cNom").Value.ToString(), "V" & num)
            Dim typeLibelle = If(row.Cells("cType").Value IsNot Nothing, row.Cells("cType").Value.ToString(), "")

            ' Trouver le périphérique dans la bibliothèque
            Dim periph As Peripherique = Nothing
            If Bibliotheque IsNot Nothing AndAlso typeLibelle <> "" AndAlso typeLibelle <> "— Non connecté —" Then
                periph = Bibliotheque.TrouverParLibelle(typeLibelle)
            End If

            ' Construire la VoieMesure selon le type de périphérique
            If periph Is Nothing OrElse typeLibelle = "— Non connecté —" Then
                ' Pas de périphérique → voie température par défaut
                c.Voies.AjouterVoieTemp(num, nom)
                voiesTemp.Add(num.ToString())
            ElseIf periph.Type = TypeMesure.TemperatureTC Then
                c.Voies.AjouterVoieTemp(num, nom)
                typeTC = periph.TypeTC
                voiesTemp.Add(num.ToString())
            ElseIf periph.UtiliseShunt OrElse periph.EstTension OrElse
                   periph.Type = TypeMesure.FrequenceImpulsions OrElse
                   periph.Type = TypeMesure.ResistancePT OrElse
                   periph.Type = TypeMesure.PuissanceW Then
                ' Voie analogique → utilise la conversion du périphérique
                Dim p As New ParamDebitmetre()
                p.RShuntOhm    = periph.RShuntOhm
                p.IminMA       = periph.SignalMin
                p.ImaxMA       = periph.SignalMax
                p.QvMin        = periph.ValMin
                p.QvMax        = periph.ValMax
                p.Unite        = periph.Unite
                p.TensionAlimV = periph.AlimCapteurV
                c.Voies.AjouterVoieDebitComplet(num, nom, p)
                voiesDebit.Add(num.ToString())
            Else
                c.Voies.AjouterVoieTemp(num, nom)
                voiesTemp.Add(num.ToString())
            End If

            ' Alarmes + surveillance débit depuis la grille
            Dim v = c.Voies.TrouverVoie(num)
            If v IsNot Nothing Then
                v.AlarmeActive       = CBool(If(row.Cells("cAlarme").Value, False))
                v.SeuilBas           = ParseD(row.Cells("cSBas").Value,  Double.NaN)
                v.SeuilHaut          = ParseD(row.Cells("cSHaut").Value, Double.NaN)
                v.SurveillanceDebit  = CBool(If(row.Cells("cSurvDebit").Value, False))
                ' Si alarme non définie en grille mais définie dans le périphérique
                If v.AlarmeActive = False AndAlso periph IsNot Nothing AndAlso periph.AlarmeActive Then
                    v.AlarmeActive = True
                    If Double.IsNaN(v.SeuilBas)  Then v.SeuilBas  = periph.SeuilBas
                    If Double.IsNaN(v.SeuilHaut) Then v.SeuilHaut = periph.SeuilHaut
                    v.HysteresisK = periph.Hysteresis
                End If
            End If
        Next

        ' Sorties
        For Each row As DataGridViewRow In _dgvSorties.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cSNum").Value IsNot Nothing, row.Cells("cSNum").Value.ToString(), ""),
                num) Then Continue For

            Dim actif    = CBool(If(row.Cells("cSActif").Value, False))
            Dim nom      = If(row.Cells("cSNom").Value   IsNot Nothing, row.Cells("cSNom").Value.ToString(),   "Sortie" & num)
            Dim modeStr  = If(row.Cells("cSMode").Value  IsNot Nothing, row.Cells("cSMode").Value.ToString(),  "Booléen (0/+Amp)")
            Dim umax     = ParseD(row.Cells("cSUMax").Value,  5.0)
            Dim seuil    = ParseD(row.Cells("cSSeuil").Value, 2.5)
            Dim mode     = If(modeStr.Contains("Analogique full"),
                              SortieAnalogique.ModePilotage.AnalogiqueFull,
                              If(modeStr.Contains("Analogique"),
                                 SortieAnalogique.ModePilotage.Analogique,
                                 SortieAnalogique.ModePilotage.Booleen))

            c.Voies.Sorties.Add(New SortieAnalogique() With {
                .Numero      = num,
                .Nom         = nom,
                .Active      = actif,
                .Mode        = mode,
                .UMax        = umax,
                .SeuilOnV    = seuil,
                .SecuriteDebit = CBool(If(row.Cells("cSSecuDebit").Value, False))
            })
        Next

        c.AppliquerConfigScan(voiesTemp, voiesDebit, typeTC)
        RaiseEvent VoiesAppliquees(Me, c)
        RaiseEvent StatutChange(Me,
            String.Format("[C{0}] {1} voie(s), {2} sortie(s) configurée(s)",
                NumeroCentrale,
                c.Voies.Voies.Count,
                c.Voies.SortiesActives().Count), False)
    End Sub

    ''' <summary>
    ''' Propage la configuration de la grille vers le Gestionnaire (voies + sorties)
    ''' SANS envoyer les commandes SCPI au Keithley.
    ''' Appeler au démarrage pour que les voies soient disponibles avant connexion.
    ''' </summary>
    Public Sub PropagerVersGestionnaire()
        If Gestionnaire Is Nothing Then Return
        Dim c = Gestionnaire.ObtenirCentrale(NumeroCentrale)
        If c Is Nothing Then Return
        c.Voies.Voies.Clear()
        c.Voies.Sorties.Clear()
        Dim voiesTemp  As New List(Of String)
        Dim voiesDebit As New List(Of String)
        Dim typeTC = "K"
        ' Voies
        For Each row As DataGridViewRow In _dgvVoies.Rows
            Dim num As Integer
            If Not Integer.TryParse(If(row.Cells("cNum").Value IsNot Nothing,
                row.Cells("cNum").Value.ToString(), ""), num) Then Continue For
            Dim actif = CBool(If(row.Cells("cActif").Value, False))
            If Not actif Then Continue For
            Dim nom = If(row.Cells("cNom").Value IsNot Nothing,
                row.Cells("cNom").Value.ToString(), "V" & num)
            Dim typeLibelle = If(row.Cells("cType").Value IsNot Nothing,
                row.Cells("cType").Value.ToString(), "")
            Dim periph As Peripherique = Nothing
            If Bibliotheque IsNot Nothing AndAlso typeLibelle <> "" AndAlso
               typeLibelle <> "— Non connecté —" Then
                periph = Bibliotheque.TrouverParLibelle(typeLibelle)
            End If
            If periph Is Nothing OrElse typeLibelle = "— Non connecté —" Then
                c.Voies.AjouterVoieTemp(num, nom) : voiesTemp.Add(num.ToString())
            ElseIf periph.Type = TypeMesure.TemperatureTC Then
                c.Voies.AjouterVoieTemp(num, nom)
                typeTC = periph.TypeTC : voiesTemp.Add(num.ToString())
            ElseIf periph.UtiliseShunt OrElse periph.EstTension OrElse
                   periph.Type = TypeMesure.FrequenceImpulsions OrElse
                   periph.Type = TypeMesure.ResistancePT OrElse
                   periph.Type = TypeMesure.PuissanceW Then
                Dim p As New ParamDebitmetre()
                p.RShuntOhm = periph.RShuntOhm : p.IminMA = periph.SignalMin
                p.ImaxMA = periph.SignalMax : p.QvMin = periph.ValMin
                p.QvMax = periph.ValMax : p.Unite = periph.Unite
                p.TensionAlimV = periph.AlimCapteurV
                c.Voies.AjouterVoieDebitComplet(num, nom, p) : voiesDebit.Add(num.ToString())
            Else
                c.Voies.AjouterVoieTemp(num, nom) : voiesTemp.Add(num.ToString())
            End If
            Dim v = c.Voies.TrouverVoie(num)
            If v IsNot Nothing Then
                v.AlarmeActive = CBool(If(row.Cells("cAlarme").Value, False))
                v.SeuilBas     = ParseD(row.Cells("cSBas").Value,  Double.NaN)
                v.SeuilHaut    = ParseD(row.Cells("cSHaut").Value, Double.NaN)
                v.SurveillanceDebit = CBool(If(row.Cells("cSurvDebit").Value, False))
            End If
        Next
        ' Sorties
        For Each row As DataGridViewRow In _dgvSorties.Rows
            Dim num As Integer
            If Not Integer.TryParse(If(row.Cells("cSNum").Value IsNot Nothing,
                row.Cells("cSNum").Value.ToString(), ""), num) Then Continue For
            Dim actif = CBool(If(row.Cells("cSActif").Value, False))
            Dim nom   = If(row.Cells("cSNom").Value IsNot Nothing,
                row.Cells("cSNom").Value.ToString(), "Sortie" & num)
            Dim modeStr = If(row.Cells("cSMode").Value IsNot Nothing,
                row.Cells("cSMode").Value.ToString(), "Booléen (0/+Amp)")
            Dim mode = If(modeStr.Contains("Analogique full"),
                SortieAnalogique.ModePilotage.AnalogiqueFull,
                If(modeStr.Contains("Analogique"),
                   SortieAnalogique.ModePilotage.Analogique,
                   SortieAnalogique.ModePilotage.Booleen))
            c.Voies.Sorties.Add(New SortieAnalogique() With {
                .Numero = num, .Nom = nom, .Active = actif, .Mode = mode,
                .UMax = ParseD(row.Cells("cSUMax").Value, 5.0),
                .SeuilOnV = ParseD(row.Cells("cSSeuil").Value, 2.5),
                .SecuriteDebit = CBool(If(row.Cells("cSSecuDebit").Value, False))})
        Next
        RaiseEvent VoiesAppliquees(Me, c)
    End Sub

    ' ─── Sauvegarde ───────────────────────────────────────────────────────────

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        BtnAppliquer_Click(sender, e)
        SauverGrilleDansConfig()
        Try
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "[C" & NumeroCentrale & "] Voies sauvegardées.", False)
        Catch ex As Exception
            MsgBox(ex.Message, MsgBoxStyle.Critical)
        End Try
    End Sub

    Private Sub BtnValeursBrutes_Click(sender As Object, e As EventArgs)
        Dim c = If(Gestionnaire IsNot Nothing,
                   Gestionnaire.ObtenirCentrale(NumeroCentrale),
                   Nothing)
        If c Is Nothing Then
            MsgBox("Centrale non disponible. Vérifiez la configuration.",
                   MsgBoxStyle.Exclamation, "Valeurs brutes")
            Return
        End If
        Using frm As New FormValeursBrutes(c, Gestionnaire)
            frm.ShowDialog(_btnValeursBrutes.FindForm())
        End Using
    End Sub

    Private Sub SauverGrilleDansConfig()
        Dim sec = "Centrale" & NumeroCentrale & "_Voies"
        Config.Set_(sec, "NbCartes", CInt(_numNbCartes.Value))

        ' Sauvegarder les config cartes
        For Each cfg In _configCartes
            cfg.SauverDansConfig(Config, "Centrale" & NumeroCentrale)
        Next

        For Each row As DataGridViewRow In _dgvVoies.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cNum").Value IsNot Nothing, row.Cells("cNum").Value.ToString(), ""), num) Then Continue For
            Dim cle = "V" & num
            Config.Set_(sec, cle & "_Actif",     CBool(If(row.Cells("cActif").Value, False)))
            Config.Set_(sec, cle & "_Nom",       CellStr(row, "cNom"))
            Config.Set_(sec, cle & "_Type",      CellStr(row, "cType"))
            Config.Set_(sec, cle & "_Alarme",    CBool(If(row.Cells("cAlarme").Value, False)))
            Config.Set_(sec, cle & "_SBas",      CellStr(row, "cSBas"))
            Config.Set_(sec, cle & "_SHaut",     CellStr(row, "cSHaut"))
            Config.Set_(sec, cle & "_SurvDebit", CBool(If(row.Cells("cSurvDebit").Value, False)))
        Next

        For Each row As DataGridViewRow In _dgvSorties.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cSNum").Value IsNot Nothing, row.Cells("cSNum").Value.ToString(), ""), num) Then Continue For
            Dim cle = "S" & num
            Config.Set_(sec, cle & "_Actif",     CBool(If(row.Cells("cSActif").Value, False)))
            Config.Set_(sec, cle & "_Nom",       CellStr(row, "cSNom"))
            Config.Set_(sec, cle & "_Mode",      CellStr(row, "cSMode"))
            Config.Set_(sec, cle & "_UMax",      CellStr(row, "cSUMax"))
            Config.Set_(sec, cle & "_Seuil",     CellStr(row, "cSSeuil"))
            Config.Set_(sec, cle & "_SecuDebit", CBool(If(row.Cells("cSSecuDebit").Value, False)))
        Next
    End Sub

    ' ─── Chargement ───────────────────────────────────────────────────────────

    Public Sub ChargerDepuisConfig()
        If _chargement Then Return
        _chargement = True
        Try
            Dim sec = "Centrale" & NumeroCentrale & "_Voies"
            Dim nb  = Config.GetInt(sec, "NbCartes", 1)

            ' Charger les config cartes
            _configCartes.Clear()
            For carte As Integer = 1 To nb
                Dim cfg As New ConfigCarte(carte)
                cfg.ChargerDepuisConfig(Config, "Centrale" & NumeroCentrale)
                _configCartes.Add(cfg)
            Next

            ' Affecter sans déclencher NbCartes_Changed (flag actif)
            _numNbCartes.Value = nb
            RebuildPanneauCartes()

            ' Recharger les valeurs dans les contrôles cartes
            For carte As Integer = 1 To nb
                If _ctrlCartes.ContainsKey(carte) Then
                    Dim idxCarte As Integer = 0
                    If _configCartes(carte - 1).Type = TypeCarte.Module7700 Then idxCarte = 1
                    If _configCartes(carte - 1).Type = TypeCarte.Autre Then idxCarte = 2
                    _ctrlCartes(carte).CmbType.SelectedIndex = idxCarte
                    _ctrlCartes(carte).TxtEntrees.Text = _configCartes(carte - 1).PlageEntrees.TexteOriginal
                    _ctrlCartes(carte).TxtSorties.Text = _configCartes(carte - 1).PlageSorties.TexteOriginal
                End If
            Next

            RemplirGrilles()

        For Each row As DataGridViewRow In _dgvVoies.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cNum").Value IsNot Nothing, row.Cells("cNum").Value.ToString(), ""), num) Then Continue For
            Dim cle    = "V" & num
            Dim actStr = Config.Get_(sec, cle & "_Actif", "")
            If actStr = "" Then Continue For
            row.Cells("cActif").Value     = Config.GetBool(sec, cle & "_Actif", False)
            row.Cells("cNom").Value       = Config.Get_(sec, cle & "_Nom",    "T_" & num)
            Dim typeLibelle = Config.Get_(sec, cle & "_Type", "— Non connecté —")
            Dim colType = TryCast(_dgvVoies.Columns("cType"), DataGridViewComboBoxColumn)
            If colType IsNot Nothing AndAlso colType.Items.Contains(typeLibelle) Then
                row.Cells("cType").Value = typeLibelle
            Else
                row.Cells("cType").Value = "— Non connecté —"
            End If
            row.Cells("cAlarme").Value    = Config.GetBool(sec, cle & "_Alarme",    False)
            row.Cells("cSBas").Value      = Config.Get_(sec, cle & "_SBas",   "")
            row.Cells("cSHaut").Value     = Config.Get_(sec, cle & "_SHaut",  "")
            row.Cells("cSurvDebit").Value = Config.GetBool(sec, cle & "_SurvDebit", False)
            ActualiserCouleurLigneVoie(row.Index)
        Next

        For Each row As DataGridViewRow In _dgvSorties.Rows
            Dim num As Integer
            If Not Integer.TryParse(
                If(row.Cells("cSNum").Value IsNot Nothing, row.Cells("cSNum").Value.ToString(), ""), num) Then Continue For
            Dim cle    = "S" & num
            Dim actStr = Config.Get_(sec, cle & "_Actif", "")
            If actStr = "" Then Continue For
            row.Cells("cSActif").Value     = Config.GetBool(sec, cle & "_Actif", False)
            row.Cells("cSNom").Value       = Config.Get_(sec,    cle & "_Nom",   "Sortie" & num)
            ' Compatibilité ascendante : traduire les anciennes chaînes de mode
            Dim modeVal = Config.Get_(sec, cle & "_Mode", "Booléen (0/+Amp)")
            Select Case modeVal
                Case "Booléen (0/Umax)", "Booléen (0/+Amp)"
                    modeVal = "Booléen (0/+Amp)"
                Case "Analogique (0–Umax)", "Analogique (0..+Amp)"
                    modeVal = "Analogique (0..+Amp)"
                Case "Analogique full (−Amp..+Amp)"
                    ' déjà correct
                Case Else
                    modeVal = "Booléen (0/+Amp)"   ' valeur inconnue → défaut
            End Select
            row.Cells("cSMode").Value      = modeVal
            row.Cells("cSUMax").Value      = Config.Get_(sec,    cle & "_UMax",  "5")
            row.Cells("cSSeuil").Value     = Config.Get_(sec,    cle & "_Seuil", "2.5")
            row.Cells("cSSecuDebit").Value = Config.GetBool(sec, cle & "_SecuDebit", False)
        Next
        Finally
            _chargement = False
        End Try
    End Sub

    ' ─── Utilitaires ──────────────────────────────────────────────────────────

    Private Function ParseD(cellValue As Object, defaut As Double) As Double
        If cellValue Is Nothing Then Return defaut
        Dim s = cellValue.ToString().Trim().Replace(",", ".")
        Dim d As Double
        If Double.TryParse(s, System.Globalization.NumberStyles.Float,
            System.Globalization.CultureInfo.InvariantCulture, d) Then Return d
        Return defaut
    End Function

    Private Function CellStr(row As DataGridViewRow, col As String) As String
        Return If(row.Cells(col).Value IsNot Nothing, row.Cells(col).Value.ToString(), "")
    End Function

End Class
