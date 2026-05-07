Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.IO
Imports System.Linq
Imports System.Windows.Forms
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  ÉTIQUETTE DE VALEUR
' ═══════════════════════════════════════════════════════════════════════════════

Public Class EtiquetteValeur

    Public Enum SourceType
        VoieMesure
        Sortie
    End Enum

    Public Property Id           As String = ""
    Public Property NomAffiche   As String = ""
    Public Property NomCentrale  As String = ""
    Public Property TypeSource   As SourceType = SourceType.VoieMesure
    Public Property Visible      As Boolean = False
    Public Property X            As Integer = 50
    Public Property Y            As Integer = 50
    Public Property ValeurTexte  As String = "---"
    Public Property EstAnalogique As Boolean = False  ' True = sortie analogique → afficher tension
    Public Property EnAlarme     As Boolean = False
    Public Property CouleurTexte As Color = Color.White
    Public Property CouleurFond  As Color = Color.FromArgb(160, 0, 0, 0)
    Public Property TaillePolice As Single = 10.0
    Public Property EnDeplacement As Boolean = False

    Public ReadOnly Property CleIni As String
        Get
            Return "Etiq_" & Id.Replace("-", "").Replace(" ", "_")
        End Get
    End Property

    Public Sub SauverDansConfig(cfg As ConfigManager, section As String)
        cfg.Set_(section, CleIni & "_Id",     Id)
        cfg.Set_(section, CleIni & "_Nom",    NomAffiche)
        cfg.Set_(section, CleIni & "_Centr",  NomCentrale)
        cfg.Set_(section, CleIni & "_Type",   CInt(TypeSource))
        cfg.Set_(section, CleIni & "_Vis",    Visible)
        cfg.Set_(section, CleIni & "_X",      X)
        cfg.Set_(section, CleIni & "_Y",      Y)
        cfg.Set_(section, CleIni & "_Taille", TaillePolice)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager, section As String)
        NomAffiche   = cfg.Get_(section,      CleIni & "_Nom",    "")
        NomCentrale  = cfg.Get_(section,      CleIni & "_Centr",  "")
        TypeSource   = CType(cfg.GetInt(section, CleIni & "_Type", 0), SourceType)
        Visible      = cfg.GetBool(section,   CleIni & "_Vis",    False)
        X            = cfg.GetInt(section,    CleIni & "_X",      50)
        Y            = cfg.GetInt(section,    CleIni & "_Y",      50)
        TaillePolice = CSng(cfg.GetDouble(section, CleIni & "_Taille", 10.0))
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  PANNEAU SCHEMA — affiche l'image + les étiquettes (inchangé)
' ═══════════════════════════════════════════════════════════════════════════════

Public Class PanneauSchema
    Inherits Panel

    Public Property Etiquettes  As List(Of EtiquetteValeur) = New List(Of EtiquetteValeur)()
    Public Property ImageSchema As Image = Nothing
    Public Property ModeEdition As Boolean = True

    Public Event EtiquetteDeplacee(e As EtiquetteValeur)

    Private _enDrag     As EtiquetteValeur = Nothing
    Private _offsetDrag As Point

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or
                 ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer, True)
        BackColor = Color.FromArgb(40, 44, 55)
        MinimumSize = New Size(200, 150)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.SmoothingMode     = Drawing2D.SmoothingMode.AntiAlias
        g.TextRenderingHint = Drawing.Text.TextRenderingHint.ClearTypeGridFit

        If ImageSchema IsNot Nothing Then
            Dim ratio = Math.Min(CSng(Width) / ImageSchema.Width,
                                 CSng(Height) / ImageSchema.Height)
            Dim w    = CInt(ImageSchema.Width  * ratio)
            Dim h    = CInt(ImageSchema.Height * ratio)
            Dim offX = (Width  - w) \ 2
            Dim offY = (Height - h) \ 2
            g.DrawImage(ImageSchema, offX, offY, w, h)
        Else
            g.FillRectangle(New SolidBrush(Color.FromArgb(30, 35, 48)), ClientRectangle)
            Dim msg = "Cliquez sur « Charger image » pour importer un schéma"
            Dim f   = New Font("Segoe UI", 10, FontStyle.Italic)
            Dim sz  = g.MeasureString(msg, f)
            g.DrawString(msg, f, Brushes.DimGray,
                         (Width - sz.Width) / 2, (Height - sz.Height) / 2)
        End If

        For Each etiq In Etiquettes.Where(Function(x) x.Visible)
            DessinerEtiquette(g, etiq)
        Next
    End Sub

    Private Sub DessinerEtiquette(g As Graphics, etiq As EtiquetteValeur)
        Dim font  = New Font("Segoe UI", etiq.TaillePolice, FontStyle.Bold)
        Dim texte = etiq.NomAffiche & " : " & etiq.ValeurTexte
        Dim sz    = g.MeasureString(texte, font)
        Dim rect  = New RectangleF(etiq.X - 4, etiq.Y - 4, sz.Width + 8, sz.Height + 6)

        Dim coulFond = If(etiq.EnAlarme, Color.FromArgb(180, 180, 30, 30), etiq.CouleurFond)
        Using br = New SolidBrush(coulFond)
            g.FillRoundedRectangle(br, rect, 4)
        End Using

        If ModeEdition OrElse etiq.EnAlarme Then
            Dim coulBord = If(etiq.EnAlarme, Color.Red,
                           If(etiq.EnDeplacement, Color.Yellow, Color.FromArgb(100, 255, 255, 255)))
            g.DrawRoundedRectangle(New Pen(coulBord, If(etiq.EnDeplacement, 2.0F, 1.0F)), rect, 4)
        End If

        Dim coulTxt = If(etiq.EnAlarme, Color.Yellow, etiq.CouleurTexte)
        g.DrawString(texte, font, New SolidBrush(coulTxt), etiq.X, etiq.Y)

        If ModeEdition Then
            g.FillEllipse(Brushes.White, etiq.X - 6, etiq.Y + sz.Height / 2 - 4, 8, 8)
        End If
    End Sub

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        If Not ModeEdition Then Return
        For Each etiq In Etiquettes.Where(Function(x) x.Visible).Reverse()
            Dim font = New Font("Segoe UI", etiq.TaillePolice, FontStyle.Bold)
            Using g = CreateGraphics()
                Dim sz   = g.MeasureString(etiq.NomAffiche & " : " & etiq.ValeurTexte, font)
                Dim rect = New RectangleF(etiq.X - 4, etiq.Y - 4, sz.Width + 8, sz.Height + 6)
                If rect.Contains(e.Location) Then
                    _enDrag            = etiq
                    _offsetDrag        = New Point(e.X - etiq.X, e.Y - etiq.Y)
                    etiq.EnDeplacement = True
                    Invalidate()
                    Return
                End If
            End Using
        Next
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        If _enDrag Is Nothing Then Return
        _enDrag.X = Math.Max(0, Math.Min(Width  - 20, e.X - _offsetDrag.X))
        _enDrag.Y = Math.Max(0, Math.Min(Height - 20, e.Y - _offsetDrag.Y))
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        If _enDrag Is Nothing Then Return
        _enDrag.EnDeplacement = False
        RaiseEvent EtiquetteDeplacee(_enDrag)
        _enDrag = Nothing
        Invalidate()
    End Sub

    Public Sub MettreAJour()
        Invalidate()
    End Sub

End Class

' ─── Extension GDI+ ────────────────────────────────────────────────────────────

Module ExtensionsGDI
    <System.Runtime.CompilerServices.Extension>
    Public Sub FillRoundedRectangle(g As Graphics, br As Brush, rect As RectangleF, radius As Single)
        Using path As New Drawing2D.GraphicsPath()
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90)
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90)
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90)
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90)
            path.CloseFigure()
            g.FillPath(br, path)
        End Using
    End Sub

    <System.Runtime.CompilerServices.Extension>
    Public Sub DrawRoundedRectangle(g As Graphics, pen As Pen, rect As RectangleF, radius As Single)
        Using path As New Drawing2D.GraphicsPath()
            path.AddArc(rect.X, rect.Y, radius * 2, radius * 2, 180, 90)
            path.AddArc(rect.Right - radius * 2, rect.Y, radius * 2, radius * 2, 270, 90)
            path.AddArc(rect.Right - radius * 2, rect.Bottom - radius * 2, radius * 2, radius * 2, 0, 90)
            path.AddArc(rect.X, rect.Bottom - radius * 2, radius * 2, radius * 2, 90, 90)
            path.CloseFigure()
            g.DrawPath(pen, path)
        End Using
    End Sub
End Module

' ═══════════════════════════════════════════════════════════════════════════════
'  PANNEAU SCHEMA COMPLET — un sous-onglet complet (barre + split + grille)
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Encapsule un sous-onglet Système complet :
''' barre d'outils, grille étiquettes, panneau schéma.
''' Plusieurs instances = plusieurs sous-onglets indépendants.
''' </summary>
Public Class PanneauSchemaComplet

    ' ─── Propriétés publiques ─────────────────────────────────────────────────

    Public Property Nom         As String = "Schéma"
    Public Property Config      As ConfigManager
    Public Property Gestionnaire As GestionnaireMultiCentrale
    Public Property GestCalculs  As GestionnaireCalculs = Nothing

    ''' <summary>Section INI dédiée à ce sous-onglet (ex: "Systeme_0").</summary>
    Public Property SectionIni  As String = "Systeme_0"

    ' ─── Données ─────────────────────────────────────────────────────────────

    Private _etiquettes  As New List(Of EtiquetteValeur)
    Private _cheminImage As String = ""

    ' ─── Contrôles ───────────────────────────────────────────────────────────

    Private _schema         As New PanneauSchema()
    Private _dgvEtiquettes  As New DataGridView()
    Private _btnChargerImg  As New Button()
    Private _btnSauver      As New Button()
    Private _btnRafraichir  As New Button()
    Private _chkModeEdition As New CheckBox()
    Private _lblImageInfo   As New Label()
    Private _split          As New SplitContainer()
    Private _btnMasquerTab  As New Button()

    ' ─── Événements ──────────────────────────────────────────────────────────

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)
    Public Event DemandeNotification(sender As Object)

    ' ─── Construction ────────────────────────────────────────────────────────

    Public Function ConstruirePanel() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' ── Barre d'outils ──
        Dim tb As New FlowLayoutPanel() With {
            .Dock         = DockStyle.Top,
            .AutoSize     = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding      = New Padding(6, 4, 6, 4)
        }

        _btnChargerImg.Text      = "📂 Charger image"
        _btnChargerImg.BackColor = Color.FromArgb(40, 110, 175)
        _btnChargerImg.ForeColor = Color.White
        _btnChargerImg.FlatStyle = FlatStyle.Flat
        _btnChargerImg.Width     = 140
        _btnChargerImg.Height    = 28
        _btnChargerImg.Margin    = New Padding(0, 0, 8, 0)

        _chkModeEdition.Text    = "Mode édition"
        _chkModeEdition.Checked = True
        _chkModeEdition.Margin  = New Padding(4, 4, 8, 0)
        _chkModeEdition.Font    = New Font("Segoe UI", 9)
        _chkModeEdition.AutoSize = True

        _btnMasquerTab.Text      = "◀ Masquer tableau"
        _btnMasquerTab.BackColor = Color.FromArgb(70, 80, 100)
        _btnMasquerTab.ForeColor = Color.White
        _btnMasquerTab.FlatStyle = FlatStyle.Flat
        _btnMasquerTab.Width     = 140
        _btnMasquerTab.Height    = 28
        _btnMasquerTab.Margin    = New Padding(0, 0, 8, 0)

        _btnRafraichir.Text      = "↺ Actualiser liste"
        _btnRafraichir.BackColor = Color.FromArgb(70, 100, 140)
        _btnRafraichir.ForeColor = Color.White
        _btnRafraichir.FlatStyle = FlatStyle.Flat
        _btnRafraichir.Width     = 135
        _btnRafraichir.Height    = 28
        _btnRafraichir.Margin    = New Padding(0, 0, 8, 0)

        _btnSauver.Text      = "💾 Sauvegarder"
        _btnSauver.BackColor = Color.FromArgb(60, 65, 80)
        _btnSauver.ForeColor = Color.White
        _btnSauver.FlatStyle = FlatStyle.Flat
        _btnSauver.Width     = 120
        _btnSauver.Height    = 28
        _btnSauver.Margin    = New Padding(0, 0, 8, 0)

        _lblImageInfo.AutoSize  = True
        _lblImageInfo.Text      = "Aucune image chargée"
        _lblImageInfo.ForeColor = Color.Gray
        _lblImageInfo.Font      = New Font("Segoe UI", 8, FontStyle.Italic)
        _lblImageInfo.Margin    = New Padding(12, 8, 0, 0)

        Dim btnNotif As New Button() With {
            .Text      = "📌 Notification",
            .BackColor = Color.FromArgb(200, 110, 0),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Width     = 145,
            .Height    = 28,
            .Margin    = New Padding(0, 0, 0, 0)
        }
        AddHandler btnNotif.Click, Sub(s, e) RaiseEvent DemandeNotification(Me)

        tb.Controls.AddRange({_btnChargerImg, _chkModeEdition, _btnMasquerTab,
                               _btnRafraichir, _btnSauver, btnNotif, _lblImageInfo})

        ' ── Split tableau / schéma ──
        _split.Dock = DockStyle.Fill

        Dim pnlGauche As New Panel() With {.Dock = DockStyle.Fill}
        Dim lblTitre As New Label() With {
            .Text      = "VOIES, SORTIES ET VARIABLES CALCULÉES",
            .Dock      = DockStyle.Top,
            .Height    = 22,
            .Font      = New Font("Segoe UI", 8, FontStyle.Bold),
            .ForeColor = Color.FromArgb(60, 100, 160),
            .Padding   = New Padding(4, 4, 0, 0)
        }
        Dim lblAide As New Label() With {
            .Text      = "Cochez Affichage, puis glissez l'étiquette sur le schéma.",
            .Dock      = DockStyle.Top,
            .Height    = 18,
            .Font      = New Font("Segoe UI", 7.5, FontStyle.Italic),
            .ForeColor = Color.Gray,
            .Padding   = New Padding(4, 0, 0, 0)
        }

        ConstruireGrille()
        pnlGauche.Controls.Add(_dgvEtiquettes)
        pnlGauche.Controls.Add(lblAide)
        pnlGauche.Controls.Add(lblTitre)

        _schema.Dock = DockStyle.Fill
        AddHandler _schema.Resize,              Sub(s, e) _schema.Invalidate()
        AddHandler _schema.EtiquetteDeplacee,   AddressOf Schema_EtiquetteDeplacee

        _split.Panel1.Controls.Add(pnlGauche)
        _split.Panel2.Controls.Add(_schema)

        pnl.Controls.Add(_split)
        pnl.Controls.Add(tb)

        ' Événements
        AddHandler _btnChargerImg.Click,  AddressOf BtnChargerImg_Click
        AddHandler _btnSauver.Click,      AddressOf BtnSauver_Click
        AddHandler _btnRafraichir.Click,  AddressOf BtnRafraichir_Click
        AddHandler _chkModeEdition.CheckedChanged, Sub(s, e)
            _schema.ModeEdition = _chkModeEdition.Checked
            _schema.Invalidate()
        End Sub
        AddHandler _btnMasquerTab.Click, Sub(s, e)
            _split.Panel1Collapsed = Not _split.Panel1Collapsed
            _btnMasquerTab.Text = If(_split.Panel1Collapsed, "▶ Afficher tableau", "◀ Masquer tableau")
        End Sub

        Return pnl
    End Function

    Private Sub ConstruireGrille()
        _dgvEtiquettes.Dock                  = DockStyle.Fill
        _dgvEtiquettes.AllowUserToAddRows    = False
        _dgvEtiquettes.AllowUserToDeleteRows = False
        _dgvEtiquettes.RowHeadersVisible     = False
        _dgvEtiquettes.AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill
        _dgvEtiquettes.EditMode              = DataGridViewEditMode.EditOnKeystrokeOrF2
        _dgvEtiquettes.SelectionMode         = DataGridViewSelectionMode.FullRowSelect
        _dgvEtiquettes.Font                  = New Font("Segoe UI", 8.5)
        _dgvEtiquettes.BackgroundColor       = Color.White

        _dgvEtiquettes.Columns.AddRange({
            New DataGridViewTextBoxColumn() With {
                .Name = "cNom", .HeaderText = "Nom voie / dispositif",
                .ReadOnly = True, .Width = 180
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cCentrale", .HeaderText = "Centrale",
                .ReadOnly = True, .Width = 75
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cCarte", .HeaderText = "Carte",
                .ReadOnly = True, .Width = 50
            },
            New DataGridViewCheckBoxColumn() With {
                .Name = "cAffiche", .HeaderText = "Affichage", .Width = 70
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cX", .HeaderText = "X (px)", .Width = 60
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cY", .HeaderText = "Y (px)", .Width = 60
            },
            New DataGridViewTextBoxColumn() With {
                .Name = "cTaille", .HeaderText = "Police", .Width = 55
            }
        })

        _dgvEtiquettes.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 240, 255)
        _dgvEtiquettes.ColumnHeadersDefaultCellStyle.Font = New Font("Segoe UI", 8, FontStyle.Bold)
        _dgvEtiquettes.EnableHeadersVisualStyles = False

        AddHandler _dgvEtiquettes.CellValueChanged, AddressOf Grille_CellValueChanged
        AddHandler _dgvEtiquettes.CurrentCellDirtyStateChanged, Sub(s, e)
            If _dgvEtiquettes.IsCurrentCellDirty Then
                _dgvEtiquettes.CommitEdit(DataGridViewDataErrorContexts.Commit)
            End If
        End Sub
    End Sub

    ' ─── Alimentation ────────────────────────────────────────────────────────

    Public Sub ActualiserListeDepuisGestionnaire()
        If Gestionnaire Is Nothing Then Return
        Dim existantes = _etiquettes.ToDictionary(Function(e) e.Id)
        Dim nouvelles  As New List(Of EtiquetteValeur)

        For Each c In Gestionnaire.Centrales
            For Each v In c.Voies.Voies.Where(Function(x) x.Active)
                Dim id = HistoriqueMultiCentrale.CleVoie(c.Numero, v.Numero)
                Dim etiq As EtiquetteValeur
                If existantes.ContainsKey(id) Then
                    etiq = existantes(id)
                Else
                    etiq = New EtiquetteValeur() With {
                        .Id          = id,
                        .NomAffiche  = v.Nom & " (" & v.Unite & ")",
                        .NomCentrale = c.NomAffiche,
                        .TypeSource  = EtiquetteValeur.SourceType.VoieMesure
                    }
                End If
                nouvelles.Add(etiq)
            Next
            For Each s In c.Voies.SortiesActives()
                Dim id = HistoriqueMultiCentrale.CleSortie(c.Numero, s.Numero)
                Dim estAnal = (s.Mode = SortieAnalogique.ModePilotage.Analogique OrElse
                               s.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull)
                Dim etiq As EtiquetteValeur
                If existantes.ContainsKey(id) Then
                    etiq = existantes(id)
                    etiq.EstAnalogique = estAnal
                Else
                    etiq = New EtiquetteValeur() With {
                        .Id            = id,
                        .NomAffiche    = s.Nom,
                        .NomCentrale   = c.NomAffiche,
                        .TypeSource    = EtiquetteValeur.SourceType.Sortie,
                        .EstAnalogique = estAnal,
                        .CouleurFond   = Color.FromArgb(160, 20, 60, 20)
                    }
                End If
                nouvelles.Add(etiq)
            Next
        Next

        ' Voies calculées
        If GestCalculs IsNot Nothing Then
            For Each vc In GestCalculs.Voies.Where(Function(v) v.Active)
                Dim id = vc.CleHistorique
                Dim etiq As EtiquetteValeur
                If existantes.ContainsKey(id) Then
                    etiq = existantes(id)
                Else
                    etiq = New EtiquetteValeur() With {
                        .Id          = id,
                        .NomAffiche  = "[Calcul] " & vc.Nom & " (" & vc.Unite & ")",
                        .NomCentrale = "Calcul",
                        .TypeSource  = EtiquetteValeur.SourceType.VoieMesure,
                        .CouleurFond = Color.FromArgb(160, 20, 60, 120)
                    }
                End If
                nouvelles.Add(etiq)
            Next
        End If

        _etiquettes            = nouvelles
        _schema.Etiquettes     = _etiquettes
        RemplirGrille()
    End Sub

    Private Sub RemplirGrille()
        _dgvEtiquettes.Rows.Clear()
        For Each etiq In _etiquettes
            Dim numCarte = ""
            Dim mC = System.Text.RegularExpressions.Regex.Match(etiq.Id, "[VS](\d+)$")
            If mC.Success Then
                Dim numVoie As Integer
                If Integer.TryParse(mC.Groups(1).Value, numVoie) Then
                    numCarte = "C" & ((numVoie \ 100)).ToString()
                End If
            End If
            Dim idx = _dgvEtiquettes.Rows.Add(
                etiq.NomAffiche, etiq.NomCentrale, numCarte,
                etiq.Visible, etiq.X.ToString(), etiq.Y.ToString(),
                etiq.TaillePolice.ToString("F0"))
            _dgvEtiquettes.Rows(idx).Tag = etiq.Id
            If etiq.TypeSource = EtiquetteValeur.SourceType.Sortie Then
                _dgvEtiquettes.Rows(idx).DefaultCellStyle.BackColor = Color.FromArgb(255, 248, 240)
            End If
        Next
    End Sub

    ' ─── Temps réel ──────────────────────────────────────────────────────────

    Public Sub MettreAJourValeurs(historique As HistoriqueMultiCentrale)
        If historique Is Nothing Then Return
        For Each etiq In _etiquettes.Where(Function(e) e.Visible)
            Dim serie = historique.ObtenirSerie(etiq.Id)
            If serie.Count = 0 Then Continue For
            Dim dernier = serie.Last()
            If etiq.TypeSource = EtiquetteValeur.SourceType.Sortie Then
                If etiq.EstAnalogique Then
                    etiq.ValeurTexte = dernier.Valeur.ToString("F2") & " V"
                Else
                    etiq.ValeurTexte = If(dernier.ValeurGraphiqueB >= 0.5, "ON", "OFF")
                End If
            ElseIf dernier.EnErreur OrElse Double.IsNaN(dernier.Valeur) Then
                etiq.ValeurTexte = "ERR"
            Else
                etiq.ValeurTexte = dernier.Valeur.ToString("F2")
            End If
            etiq.EnAlarme = dernier.EnAlarme
        Next
        _schema.MettreAJour()
    End Sub

    ' ─── Grille ↔ étiquettes ─────────────────────────────────────────────────

    Private Sub Grille_CellValueChanged(sender As Object, e As DataGridViewCellEventArgs)
        If e.RowIndex < 0 Then Return
        Dim row  = _dgvEtiquettes.Rows(e.RowIndex)
        Dim id   = If(row.Tag IsNot Nothing, row.Tag.ToString(), "")
        Dim etiq = _etiquettes.FirstOrDefault(Function(x) x.Id = id)
        If etiq Is Nothing Then Return
        Dim col = _dgvEtiquettes.Columns(e.ColumnIndex).Name
        Select Case col
            Case "cAffiche"
                etiq.Visible = CBool(If(row.Cells("cAffiche").Value, False))
            Case "cX"
                Dim v As Integer
                If Integer.TryParse(CellStr(row, "cX"), v) Then etiq.X = Math.Max(0, v)
            Case "cY"
                Dim v As Integer
                If Integer.TryParse(CellStr(row, "cY"), v) Then etiq.Y = Math.Max(0, v)
            Case "cTaille"
                Dim v As Single
                If Single.TryParse(CellStr(row, "cTaille"),
                    System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, v) Then
                    etiq.TaillePolice = Math.Max(7, Math.Min(20, v))
                End If
        End Select
        _schema.Invalidate()
    End Sub

    Private Sub Schema_EtiquetteDeplacee(etiq As EtiquetteValeur)
        For Each row As DataGridViewRow In _dgvEtiquettes.Rows
            If row.Tag IsNot Nothing AndAlso row.Tag.ToString() = etiq.Id Then
                row.Cells("cX").Value = etiq.X.ToString()
                row.Cells("cY").Value = etiq.Y.ToString()
                Exit For
            End If
        Next
    End Sub

    ' ─── Boutons ─────────────────────────────────────────────────────────────

    Private Sub BtnChargerImg_Click(sender As Object, e As EventArgs)
        Using dlg As New OpenFileDialog() With {
            .Title  = "Charger un schéma de principe",
            .Filter = "Images (*.png;*.jpg;*.jpeg;*.bmp;*.gif)|*.png;*.jpg;*.jpeg;*.bmp;*.gif|Tous|*.*"
        }
            If dlg.ShowDialog() <> DialogResult.OK Then Return
            Try
                _schema.ImageSchema = Image.FromFile(dlg.FileName)
                _cheminImage        = dlg.FileName
                _lblImageInfo.Text  = Path.GetFileName(dlg.FileName) & "  (" &
                                      _schema.ImageSchema.Width & "×" &
                                      _schema.ImageSchema.Height & " px)"
                _lblImageInfo.ForeColor = Color.FromArgb(60, 100, 160)
                _schema.Invalidate()
                RaiseEvent StatutChange(Me, "Image chargée : " & Path.GetFileName(dlg.FileName), False)
            Catch ex As Exception
                MessageBox.Show("Impossible de charger l'image : " & ex.Message,
                                "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
            End Try
        End Using
    End Sub

    Private Sub BtnRafraichir_Click(sender As Object, e As EventArgs)
        ActualiserListeDepuisGestionnaire()
        RaiseEvent StatutChange(Me, "Liste actualisée — " & _etiquettes.Count & " éléments.", False)
    End Sub

    Private Sub BtnSauver_Click(sender As Object, e As EventArgs)
        SauverDansConfig()
        Try
            Config.Sauvegarder()
            RaiseEvent StatutChange(Me, "Schéma « " & Nom & " » sauvegardé.", False)
        Catch ex As Exception
            MessageBox.Show(ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error)
        End Try
    End Sub

    ' ─── Persistance ─────────────────────────────────────────────────────────

    Public Sub SauverDansConfig()
        Config.Set_(SectionIni, "Nom",          Nom)
        Config.Set_(SectionIni, "ImageChemin",  _cheminImage)
        Config.Set_(SectionIni, "NbEtiquettes", _etiquettes.Count)
        For i As Integer = 0 To _etiquettes.Count - 1
            Config.Set_(SectionIni, "EtiqId_" & i, _etiquettes(i).Id)
            _etiquettes(i).SauverDansConfig(Config, SectionIni)
        Next
    End Sub

    Public Sub ChargerDepuisConfig()
        Nom          = Config.Get_(SectionIni, "Nom", Nom)
        _cheminImage = Config.Get_(SectionIni, "ImageChemin", "")
        If _cheminImage <> "" AndAlso File.Exists(_cheminImage) Then
            Try
                _schema.ImageSchema = Image.FromFile(_cheminImage)
                _lblImageInfo.Text  = Path.GetFileName(_cheminImage)
                _lblImageInfo.ForeColor = Color.FromArgb(60, 100, 160)
            Catch
            End Try
        End If
        Dim nb = Config.GetInt(SectionIni, "NbEtiquettes", 0)
        _etiquettes.Clear()
        For i As Integer = 0 To nb - 1
            Dim id = Config.Get_(SectionIni, "EtiqId_" & i, "")
            If id = "" Then Continue For
            Dim etiq As New EtiquetteValeur() With {.Id = id}
            etiq.ChargerDepuisConfig(Config, SectionIni)
            _etiquettes.Add(etiq)
        Next
        _schema.Etiquettes = _etiquettes
        RemplirGrille()
    End Sub

    ' ─── Utilitaire ──────────────────────────────────────────────────────────

    Private Function CellStr(row As DataGridViewRow, col As String) As String
        Return If(row.Cells(col).Value IsNot Nothing, row.Cells(col).Value.ToString(), "")
    End Function

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  ONGLET SYSTÈME — gestionnaire de sous-onglets
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Onglet Système principal.
''' Contient un TabControl avec N sous-onglets, chacun étant un PanneauSchemaComplet.
''' Boutons : Ajouter un sous-onglet, Renommer, Supprimer.
''' </summary>
Public Class OngletSysteme

    Public Property Config       As ConfigManager
    Public Property Gestionnaire As GestionnaireMultiCentrale
    Public Property GestCalculs  As GestionnaireCalculs = Nothing

    Private _tabSousOnglets As New TabControl()
    Private _panneaux       As New List(Of PanneauSchemaComplet)

    Public Event StatutChange(sender As Object, message As String, estErreur As Boolean)
    Public Event DemandeNotification(sender As Object)

    ' ─── Construction ────────────────────────────────────────────────────────

    Public Function ConstruirePanel() As Control
        Dim pnl As New Panel() With {.Dock = DockStyle.Fill}

        ' ── Barre de gestion des sous-onglets ──
        Dim tbGestion As New FlowLayoutPanel() With {
            .Dock         = DockStyle.Top,
            .AutoSize     = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink,
            .Padding      = New Padding(6, 4, 6, 2),
            .BackColor    = Color.FromArgb(35, 38, 52)
        }

        Dim btnAjouter As New Button() With {
            .Text      = "＋ Nouveau schéma",
            .BackColor = Color.FromArgb(40, 110, 60),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Height    = 26,
            .AutoSize  = True,
            .Margin    = New Padding(0, 0, 6, 0)
        }
        Dim btnRenommer As New Button() With {
            .Text      = "✏ Renommer",
            .BackColor = Color.FromArgb(80, 90, 50),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Height    = 26,
            .AutoSize  = True,
            .Margin    = New Padding(0, 0, 6, 0)
        }
        Dim btnSupprimer As New Button() With {
            .Text      = "✕ Supprimer",
            .BackColor = Color.FromArgb(110, 40, 40),
            .ForeColor = Color.White,
            .FlatStyle = FlatStyle.Flat,
            .Height    = 26,
            .AutoSize  = True,
            .Margin    = New Padding(0, 0, 0, 0)
        }

        Dim tt As New ToolTip()
        tt.SetToolTip(btnAjouter,   "Ajouter un nouveau sous-onglet schéma")
        tt.SetToolTip(btnRenommer,  "Renommer le sous-onglet actif")
        tt.SetToolTip(btnSupprimer, "Supprimer le sous-onglet actif (irréversible)")

        tbGestion.Controls.AddRange({btnAjouter, btnRenommer, btnSupprimer})

        ' ── TabControl ──
        _tabSousOnglets.Dock = DockStyle.Fill

        pnl.Controls.Add(_tabSousOnglets)
        pnl.Controls.Add(tbGestion)

        ' Événements gestion
        AddHandler btnAjouter.Click,   AddressOf BtnAjouter_Click
        AddHandler btnRenommer.Click,  AddressOf BtnRenommer_Click
        AddHandler btnSupprimer.Click, AddressOf BtnSupprimer_Click

        Return pnl
    End Function

    ' ─── Gestion des sous-onglets ─────────────────────────────────────────────

    Private Function AjouterSousOnglet(nom As String) As PanneauSchemaComplet
        Dim idx     = _panneaux.Count
        Dim section = "Systeme_" & idx

        Dim panneau As New PanneauSchemaComplet() With {
            .Nom          = nom,
            .Config       = Config,
            .Gestionnaire = Gestionnaire,
            .GestCalculs  = GestCalculs,
            .SectionIni   = section
        }
        AddHandler panneau.StatutChange, Sub(s, msg, err)
            RaiseEvent StatutChange(s, msg, err)
        End Sub
        AddHandler panneau.DemandeNotification, Sub(s)
            RaiseEvent DemandeNotification(s)
        End Sub

        Dim tabPage As New TabPage(nom)
        tabPage.Controls.Add(panneau.ConstruirePanel())
        tabPage.Tag = panneau

        _tabSousOnglets.TabPages.Add(tabPage)
        _panneaux.Add(panneau)

        ' Actualiser la liste depuis le gestionnaire
        panneau.ActualiserListeDepuisGestionnaire()

        Return panneau
    End Function

    Private Sub BtnAjouter_Click(sender As Object, e As EventArgs)
        Dim nom = InputBoxDemander("Nom du nouveau schéma :", "Nouveau schéma",
                                   "Schéma " & (_panneaux.Count + 1))
        If String.IsNullOrWhiteSpace(nom) Then Return
        AjouterSousOnglet(nom)
        _tabSousOnglets.SelectedIndex = _tabSousOnglets.TabPages.Count - 1
        RaiseEvent StatutChange(Me, "Sous-onglet « " & nom & " » créé.", False)
    End Sub

    Private Sub BtnRenommer_Click(sender As Object, e As EventArgs)
        If _tabSousOnglets.SelectedTab Is Nothing Then Return
        Dim panneau = TryCast(_tabSousOnglets.SelectedTab.Tag, PanneauSchemaComplet)
        If panneau Is Nothing Then Return

        Dim nouveauNom = InputBoxDemander("Nouveau nom :", "Renommer", panneau.Nom)
        If String.IsNullOrWhiteSpace(nouveauNom) Then Return

        panneau.Nom                         = nouveauNom
        _tabSousOnglets.SelectedTab.Text    = nouveauNom
        RaiseEvent StatutChange(Me, "Sous-onglet renommé en « " & nouveauNom & " ».", False)
    End Sub

    Private Sub BtnSupprimer_Click(sender As Object, e As EventArgs)
        If _tabSousOnglets.TabPages.Count <= 1 Then
            MessageBox.Show("Il doit rester au moins un sous-onglet.",
                            "Suppression impossible", MessageBoxButtons.OK, MessageBoxIcon.Warning)
            Return
        End If
        If _tabSousOnglets.SelectedTab Is Nothing Then Return
        Dim panneau = TryCast(_tabSousOnglets.SelectedTab.Tag, PanneauSchemaComplet)
        If panneau Is Nothing Then Return

        Dim rep = MessageBox.Show(
            "Supprimer le schéma « " & panneau.Nom & " » ?" & vbCrLf &
            "Les positions des étiquettes seront perdues.",
            "Confirmer la suppression",
            MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
        If rep <> DialogResult.Yes Then Return

        _panneaux.Remove(panneau)
        _tabSousOnglets.TabPages.Remove(_tabSousOnglets.SelectedTab)

        ' Renuméroter les sections INI
        RenumerotterSections()
        RaiseEvent StatutChange(Me, "Sous-onglet supprimé.", False)
    End Sub

    ''' <summary>Renuméroter les SectionIni après suppression.</summary>
    Private Sub RenumerotterSections()
        For i As Integer = 0 To _panneaux.Count - 1
            _panneaux(i).SectionIni = "Systeme_" & i
        Next
    End Sub

    ' ─── InputBox personnalisée ───────────────────────────────────────────────

    Private Shared Function InputBoxDemander(invite As String, titre As String,
                                              defaut As String) As String
        Using frm As New Form() With {
            .Text            = titre,
            .Size            = New Size(380, 130),
            .StartPosition   = FormStartPosition.CenterParent,
            .FormBorderStyle = FormBorderStyle.FixedDialog,
            .MaximizeBox     = False,
            .MinimizeBox     = False
        }
            Dim lbl As New Label() With {
                .Text     = invite,
                .Location = New Point(12, 12),
                .AutoSize = True
            }
            Dim txt As New TextBox() With {
                .Text     = defaut,
                .Location = New Point(12, 32),
                .Width    = 340,
                .Font     = New Font("Segoe UI", 10)
            }
            txt.SelectAll()
            Dim btnOK As New Button() With {
                .Text         = "OK",
                .DialogResult = DialogResult.OK,
                .Location     = New Point(196, 60),
                .Width        = 80
            }
            Dim btnAnn As New Button() With {
                .Text         = "Annuler",
                .DialogResult = DialogResult.Cancel,
                .Location     = New Point(284, 60),
                .Width        = 70
            }
            frm.Controls.AddRange({lbl, txt, btnOK, btnAnn})
            frm.AcceptButton = btnOK
            frm.CancelButton = btnAnn
            If frm.ShowDialog() = DialogResult.OK Then Return txt.Text.Trim()
            Return ""
        End Using
    End Function

    ' ─── Mise à jour temps réel (tous les sous-onglets) ──────────────────────

    Public Sub MettreAJourValeurs(historique As HistoriqueMultiCentrale)
        For Each p In _panneaux
            p.MettreAJourValeurs(historique)
        Next
    End Sub

    ''' <summary>Appelé après Appliquer dans OngletVoies — actualise la liste de chaque sous-onglet.</summary>
    Public Sub ActualiserListeDepuisGestionnaire()
        For Each p In _panneaux
            p.GestCalculs = GestCalculs   ' s'assurer que la référence est à jour
            p.ActualiserListeDepuisGestionnaire()
        Next
    End Sub

    ' ─── Persistance ─────────────────────────────────────────────────────────

    Public Sub SauverDansConfig()
        Config.Set_(ConfigManager.SEC_SYSTEME, "NbSousOnglets", _panneaux.Count)
        For i As Integer = 0 To _panneaux.Count - 1
            _panneaux(i).SectionIni = "Systeme_" & i
            _panneaux(i).SauverDansConfig()
        Next
    End Sub

    Public Sub ChargerDepuisConfig()
        _tabSousOnglets.TabPages.Clear()
        _panneaux.Clear()

        Dim nb = Config.GetInt(ConfigManager.SEC_SYSTEME, "NbSousOnglets", 0)

        If nb = 0 Then
            ' Compatibilité ascendante : ancien format avec une seule section [Systeme]
            Dim panneau = AjouterSousOnglet("Schéma principal")
            panneau.SectionIni = ConfigManager.SEC_SYSTEME
            panneau.ChargerDepuisConfig()
            Return
        End If

        For i As Integer = 0 To nb - 1
            Dim section = "Systeme_" & i
            Dim nom     = Config.Get_(section, "Nom", "Schéma " & (i + 1))
            Dim panneau = AjouterSousOnglet(nom)
            panneau.SectionIni = section
            panneau.ChargerDepuisConfig()
            ' Mettre à jour le texte de l'onglet (nom chargé depuis config)
            _tabSousOnglets.TabPages(_tabSousOnglets.TabPages.Count - 1).Text = panneau.Nom
        Next
    End Sub

End Class
