Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Threading
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  RÈGLE CONDITIONNELLE
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Règle de la forme : Si [Voie X de Centrale N] [op] [seuil] → [Action sur Sortie Y]
''' Actions : Activer (ON), Désactiver (OFF), Régler tension (V).
''' Évaluée en continu pendant le chronogramme.
''' </summary>
Public Class RegleConditionnelle

    Public Enum TypeActionRegle
        Activer       ' ON
        Desactiver    ' OFF
        ReglerTension ' valeur numérique (Analogique ou Analogique full)
    End Enum

    Public Property NumeroCentraleVoie   As Integer
    Public Property NumeroVoie           As Integer
    Public Property Operateur            As String    ' ">", ">=", "<", "<=", "="
    Public Property ValeurSeuil          As Double
    Public Property NumeroCentraleSortie As Integer
    Public Property NumeroSortie         As Integer
    Public Property TypeAction           As TypeActionRegle = TypeActionRegle.Activer
    Public Property TensionCible         As Double = 0.0   ' utilisé si TypeAction = ReglerTension

    ' Alias de compatibilité
    Public Property ActiverSortie As Boolean
        Get
            Return TypeAction = TypeActionRegle.Activer
        End Get
        Set(value As Boolean)
            TypeAction = If(value, TypeActionRegle.Activer, TypeActionRegle.Desactiver)
        End Set
    End Property

    ''' <summary>Évalue la règle et retourne True si la condition est vérifiée.</summary>
    Public Function EvaluerCondition(valeurMesuree As Double) As Boolean
        If Double.IsNaN(valeurMesuree) Then Return False
        Select Case Operateur
            Case ">"  : Return valeurMesuree >  ValeurSeuil
            Case ">=" : Return valeurMesuree >= ValeurSeuil
            Case "<"  : Return valeurMesuree <  ValeurSeuil
            Case "<=" : Return valeurMesuree <= ValeurSeuil
            Case "="  : Return Math.Abs(valeurMesuree - ValeurSeuil) < 0.001
            Case Else  : Return False
        End Select
    End Function

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  RELAIS
' ═══════════════════════════════════════════════════════════════════════════════

Public Class Relais

    Public Enum NomRelais
        Pompe
        Rechauffeur
        Aerotherme
    End Enum

    Public Property Nom           As NomRelais
    Public Property VoieKeithley  As Integer
    Public Property Etat          As Boolean = False
    Public Property LibelleAffichage As String

    ' Référence à la centrale qui possède ce relais
    Public Property NumeroCentrale As Integer = 1

    Public ReadOnly Property NomStr As String
        Get
            Select Case Nom
                Case NomRelais.Pompe
                    Return "Pompe"
                Case NomRelais.Rechauffeur
                    Return "Réchauffeur"
                Case NomRelais.Aerotherme
                    Return "Aérotherme"
                Case Else
                    Return "Relais"
            End Select
        End Get
    End Property

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  RELAIS DYNAMIQUE (sortie analogique nommée)
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Représente un relais piloté via une sortie analogique d'une centrale.
''' Utilisé par le chronogramme pour piloter les dispositifs.
''' </summary>
Public Class RelaisDynamique

    Public Property Id             As String
    Public Property NomDispositif  As String
    Public Property NumeroCentrale As Integer
    Public Property NumeroSortie   As Integer
    Public Property Etat           As Boolean = False
    ''' <summary>Tension globale par défaut pour le mode Analogique/Analogique full (V).</summary>
    Public Property TensionDefaut  As Double = 0.0
    ''' <summary>
    ''' Si True : conserver la tension de l'étape précédente quand la cellule est vide.
    ''' Si False : appliquer 0V quand la cellule est vide.
    ''' </summary>
    Public Property Maintien       As Boolean = False
    ''' <summary>Dernière tension réellement appliquée — utilisée par le mode Maintien.</summary>
    Public Property TensionCourante As Double = Double.NaN

    Public ReadOnly Property Libelle As String
        Get
            Return String.Format("[C{0}] {1}", NumeroCentrale, NomDispositif)
        End Get
    End Property

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  ÉTAPE DE CHRONOGRAMME
' ═══════════════════════════════════════════════════════════════════════════════

Public Class EtapeChronogramme

    Public Property Nom           As String = "Étape"
    Public Property DureeSecondes As Integer = 60

    ' États souhaités par ID de relais dynamique
    ' Clé = RelaisDynamique.Id, Valeur = True (ON) / False (OFF)
    Public Property EtatsRelais As New Dictionary(Of String, Boolean)

    ''' <summary>
    ''' Tensions par étape pour les sorties en mode Analogique.
    ''' Clé = RelaisDynamique.Id
    ''' Valeur = tension souhaitée (V), ou Double.NaN si utiliser la tension globale par défaut.
    ''' </summary>
    Public Property TensionsSorties As New Dictionary(Of String, Double)

    ' Compatibilité ascendante avec l'ancien système à 3 relais fixes
    Public Property PompeActive      As Boolean = False
    Public Property RechauffeurActif As Boolean = False
    Public Property AerothermeActif  As Boolean = False

    Public ReadOnly Property DureeFormatee As String
        Get
            Dim h = DureeSecondes \ 3600
            Dim m = (DureeSecondes Mod 3600) \ 60
            Dim s = DureeSecondes Mod 60
            Return String.Format("{0:D2}h {1:D2}m {2:D2}s", h, m, s)
        End Get
    End Property

    Public Overrides Function ToString() As String
        Return Nom & " — " & DureeFormatee
    End Function

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  MOTEUR DE CHRONOGRAMME MULTI-CENTRALE
' ═══════════════════════════════════════════════════════════════════════════════

Public Class MoteurChronogramme

    ' ─── Événements ─────────────────────────────────────────────────────────

    Public Event EtapeChange(sender As Object, nomEtape As String, indexEtape As Integer)
    Public Event EtatChange(sender As Object, e As EtatChronogrammeEventArgs)
    Public Event ChronogrammeTermine(sender As Object)
    Public Event SecuriteDeclenche(sender As Object, message As String)

    ' ─── Configuration ──────────────────────────────────────────────────────

    Public Property Etapes               As New List(Of EtapeChronogramme)
    Public Property DureeTotaleSecondes  As Integer = 3600
    Public Property BouclerSurDuree      As Boolean = True
    ''' <summary>Quand True, le chronogramme ne pilote pas les sorties (mode manuel prioritaire).</summary>
    Public Property ModeManuelActif      As Boolean = False

    ''' <summary>Règles conditionnelles évaluées à chaque seconde du chronogramme.</summary>
    Public Property Regles As New List(Of RegleConditionnelle)

    ' Références multi-centrales
    Public Property Gestionnaire  As GestionnaireMultiCentrale
    Public Property GestionVoies  As GestionVoies   ' compatibilité mono-centrale
    Public Property RelaisDynamiques As New List(Of RelaisDynamique)

    ' Référence mono-centrale (compatibilité)
    Public Property Keithley As KeithleyComm
    Private _relaisMono As Dictionary(Of Relais.NomRelais, Relais)

    ' ─── État ───────────────────────────────────────────────────────────────

    Public Property EnCours        As Boolean = False
    Public Property EtapeCourante  As Integer = 0
    Public Property TempsEcouleSec As Integer = 0

    Private _thread  As Thread
    Private _annuler As Boolean = False

    ' ─── Initialisation ─────────────────────────────────────────────────────

    Public Sub Initialiser(keithley As KeithleyComm,
                           relais As Dictionary(Of Relais.NomRelais, Relais))
        Keithley    = keithley
        _relaisMono = relais
    End Sub

    ''' <summary>
    ''' Construit la liste des RelaisDynamiques à partir du Gestionnaire multi-centrale.
    ''' À appeler après que les voies sont configurées.
    ''' </summary>
    Public Sub MettreAJourRelaisDynamiques()
        RelaisDynamiques.Clear()
        If Gestionnaire Is Nothing Then Return
        For Each c In Gestionnaire.Centrales
            For Each s In c.Voies.SortiesActives()
                RelaisDynamiques.Add(New RelaisDynamique() With {
                    .Id              = HistoriqueMultiCentrale.CleSortie(c.Numero, s.Numero),
                    .NomDispositif   = s.Nom,
                    .NumeroCentrale  = c.Numero,
                    .NumeroSortie    = s.Numero,
                    .Etat            = False
                })
            Next
        Next
    End Sub

    ' ─── Démarrage / Arrêt ──────────────────────────────────────────────────

    Public Sub Demarrer()
        If EnCours Then Return
        ' Réinitialiser uniquement TensionCourante (Maintien est assigné par l'appelant)
        For Each rd In RelaisDynamiques
            rd.TensionCourante = Double.NaN
        Next
        _annuler = False
        _thread  = New Thread(AddressOf BoucleExecution) With {
            .IsBackground = True, .Name = "Chronogramme"
        }
        EnCours = True
        _thread.Start()
    End Sub

    Public Sub Arreter()
        _annuler = True
        EnCours  = False
        ToutEteindre()
    End Sub

    ' ─── Boucle d'exécution ─────────────────────────────────────────────────

    Private Sub BoucleExecution()
        Dim tempsEcoule As Integer = 0
        Do
            For idxEtape As Integer = 0 To Etapes.Count - 1
                If _annuler Then GoTo Fin
                Dim etape = Etapes(idxEtape)
                EtapeCourante = idxEtape
                RaiseEvent EtapeChange(Me, etape.Nom, idxEtape)

                For s As Integer = 0 To etape.DureeSecondes - 1
                    If _annuler Then GoTo Fin
                    TempsEcouleSec = tempsEcoule + s
                    AppliquerEtape(etape)
                    Thread.Sleep(1000)
                Next
                tempsEcoule += etape.DureeSecondes

                If BouclerSurDuree AndAlso tempsEcoule >= DureeTotaleSecondes Then
                    tempsEcoule = 0
                End If
            Next
            If Not BouclerSurDuree Then Exit Do
        Loop Until _annuler

Fin:
        ToutEteindre()
        EnCours = False
        If Not _annuler Then RaiseEvent ChronogrammeTermine(Me)
    End Sub

    ' ─── Application d'une étape ─────────────────────────────────────────────

    Private Sub AppliquerEtape(etape As EtapeChronogramme)
        ' Si l'opérateur a pris le contrôle manuel des relais, ne pas écraser
        If ModeManuelActif Then Return

        ' ── 1. Vérification du débit de sécurité ─────────────────────────────
        Dim debitOK  = True
        Dim msgDebit = ""
        Dim voiesSurveill As New List(Of (Voie As VoieMesure, NomCentrale As String))

        If Gestionnaire IsNot Nothing Then
            For Each c In Gestionnaire.Centrales
                For Each v In c.Voies.Voies.Where(Function(x) x.Active AndAlso x.SurveillanceDebit)
                    voiesSurveill.Add((v, c.NomAffiche))
                Next
            Next
        End If

        For Each item In voiesSurveill
            Dim v   = item.Voie
            Dim val = v.Valeur
            If Double.IsNaN(val) OrElse v.EnErreur Then Continue For
            Dim horsPlage = False
            If Not Double.IsNaN(v.SeuilBas)  AndAlso val < v.SeuilBas  Then horsPlage = True
            If Not Double.IsNaN(v.SeuilHaut) AndAlso val > v.SeuilHaut Then horsPlage = True
            If horsPlage Then
                debitOK  = False
                msgDebit = String.Format("{0} [{1}] = {2:F2} hors plage [{3:F2} – {4:F2}]",
                    v.Nom, item.NomCentrale, val,
                    If(Double.IsNaN(v.SeuilBas),  -999, v.SeuilBas),
                    If(Double.IsNaN(v.SeuilHaut), 9999, v.SeuilHaut))
                Exit For
            End If
        Next

        If Not debitOK AndAlso msgDebit <> "" Then
            RaiseEvent SecuriteDeclenche(Me, "⚠ Débit hors plage : " & msgDebit)
        End If

        ' ── 2. Évaluer les règles conditionnelles EN PREMIER ─────────────────
        ' Les sorties pilotées par une règle active sont exclues de l'application
        ' des étapes (la règle est prioritaire).
        Dim sortiesPiloteesParRegle As New HashSet(Of String)  ' clés "CX_SYYYY"

        If Regles.Count > 0 AndAlso Gestionnaire IsNot Nothing Then
            For Each regle In Regles
                Dim c = Gestionnaire.ObtenirCentrale(regle.NumeroCentraleVoie)
                If c Is Nothing Then Continue For
                Dim v = c.Voies.TrouverVoie(regle.NumeroVoie)
                If v Is Nothing OrElse v.EnErreur OrElse Double.IsNaN(v.Valeur) Then Continue For

                Dim idSortie = HistoriqueMultiCentrale.CleSortie(regle.NumeroCentraleSortie, regle.NumeroSortie)
                Dim sortieR  = If(Gestionnaire.ObtenirCentrale(regle.NumeroCentraleSortie) IsNot Nothing,
                    Gestionnaire.ObtenirCentrale(regle.NumeroCentraleSortie).Voies.TrouverSortie(regle.NumeroSortie),
                    Nothing)

                If regle.EvaluerCondition(v.Valeur) Then
                    ' La règle est active → piloter et marquer la sortie comme gérée
                    sortiesPiloteesParRegle.Add(idSortie)
                    Select Case regle.TypeAction
                        Case RegleConditionnelle.TypeActionRegle.ReglerTension
                            Dim amp = If(sortieR IsNot Nothing, sortieR.Amplitude, 12.0)
                            Dim t   = Math.Max(-amp, Math.Min(amp, regle.TensionCible))
                            PiloterSortieAnalogique(regle.NumeroCentraleSortie, regle.NumeroSortie, t, amp)
                            If sortieR IsNot Nothing Then sortieR.TensionV = t
                            RaiseEvent SecuriteDeclenche(Me,
                                String.Format("Règle : {0} ({1:F2}) {2} {3:F2} → {4} = {5:F2} V",
                                    v.Nom, v.Valeur, regle.Operateur, regle.ValeurSeuil,
                                    If(sortieR IsNot Nothing, sortieR.Nom, "S" & regle.NumeroSortie), t))
                        Case RegleConditionnelle.TypeActionRegle.Activer
                            PiloterSortie(regle.NumeroCentraleSortie, regle.NumeroSortie, True)
                            If sortieR IsNot Nothing Then sortieR.TensionV = sortieR.UMax
                            RaiseEvent SecuriteDeclenche(Me,
                                String.Format("Règle : {0} ({1:F2}) {2} {3:F2} → {4} ON",
                                    v.Nom, v.Valeur, regle.Operateur, regle.ValeurSeuil,
                                    If(sortieR IsNot Nothing, sortieR.Nom, "S" & regle.NumeroSortie)))
                        Case RegleConditionnelle.TypeActionRegle.Desactiver
                            PiloterSortie(regle.NumeroCentraleSortie, regle.NumeroSortie, False)
                            If sortieR IsNot Nothing Then sortieR.TensionV = 0.0
                            RaiseEvent SecuriteDeclenche(Me,
                                String.Format("Règle : {0} ({1:F2}) {2} {3:F2} → {4} OFF",
                                    v.Nom, v.Valeur, regle.Operateur, regle.ValeurSeuil,
                                    If(sortieR IsNot Nothing, sortieR.Nom, "S" & regle.NumeroSortie)))
                    End Select
                Else
                    ' Condition non vérifiée → la sortie est libre pour l'étape
                    ' (ne pas ajouter à sortiesPiloteesParRegle)
                End If
            Next
        End If

        ' ── 3. Piloter les relais dynamiques (étapes), sauf ceux pris par une règle ──
        For Each rd In RelaisDynamiques
            Dim idSortie = rd.Id
            If sortiesPiloteesParRegle.Contains(idSortie) Then Continue For  ' priorité règle

            Dim centrale = If(Gestionnaire IsNot Nothing, Gestionnaire.ObtenirCentrale(rd.NumeroCentrale), Nothing)
            Dim sortie   = If(centrale IsNot Nothing, centrale.Voies.TrouverSortie(rd.NumeroSortie), Nothing)
            Dim modeAnal = sortie IsNot Nothing AndAlso
                           (sortie.Mode = SortieAnalogique.ModePilotage.Analogique OrElse
                            sortie.Mode = SortieAnalogique.ModePilotage.AnalogiqueFull)

            If modeAnal Then
                Dim tension As Double = Double.NaN
                If etape.TensionsSorties.ContainsKey(rd.Id) Then
                    tension = etape.TensionsSorties(rd.Id)
                End If
                If Double.IsNaN(tension) Then
                    ' Cellule vide : maintien = dernière tension appliquée, sinon 0
                    If rd.Maintien AndAlso Not Double.IsNaN(rd.TensionCourante) Then
                        tension = rd.TensionCourante
                    Else
                        tension = 0.0
                    End If
                End If
                Dim tMin = sortie.TensionMin
                Dim tMax = sortie.TensionMax
                tension = Math.Max(tMin, Math.Min(tMax, tension))
                PiloterSortieAnalogique(rd.NumeroCentrale, rd.NumeroSortie, tension, tMax)
                If sortie IsNot Nothing Then sortie.TensionV = tension
                rd.TensionCourante = tension   ' mémoriser pour le prochain maintien
            Else
                ' Mode Booléen
                Dim etatSouhaite As Boolean = False
                If etape.EtatsRelais.ContainsKey(rd.Id) Then
                    etatSouhaite = etape.EtatsRelais(rd.Id)
                End If

                ' Sécurité débit : la case ARRET Surveill. sécu coupe la sortie si débit hors plage
                Dim securiteActive = sortie IsNot Nothing AndAlso sortie.SecuriteDebit
                If etatSouhaite AndAlso securiteActive AndAlso Not debitOK Then
                    etatSouhaite = False
                    RaiseEvent SecuriteDeclenche(Me,
                        String.Format("{0} bloqué : débit de sécurité hors plage", rd.NomDispositif))
                End If

                If rd.Etat <> etatSouhaite Then
                    rd.Etat = etatSouhaite
                    PiloterSortie(rd.NumeroCentrale, rd.NumeroSortie, etatSouhaite)
                End If
                If sortie IsNot Nothing Then
                    sortie.TensionV = If(etatSouhaite, sortie.UMax, 0.0)
                End If
            End If
        Next

        ' ── 4. Compatibilité mono-centrale (3 relais fixes) ──────────────────
        If _relaisMono IsNot Nothing AndAlso Keithley IsNot Nothing AndAlso Keithley.IsConnected Then
            PiloterRelaiMono(Relais.NomRelais.Pompe, etape.PompeActive)
            Dim rechOK = etape.RechauffeurActif AndAlso debitOK
            If etape.RechauffeurActif AndAlso Not debitOK Then
                RaiseEvent SecuriteDeclenche(Me, "Réchauffeur bloqué : débit insuffisant")
            End If
            PiloterRelaiMono(Relais.NomRelais.Rechauffeur, rechOK)
            Dim aeroOK = etape.AerothermeActif AndAlso debitOK
            If etape.AerothermeActif AndAlso Not debitOK Then
                RaiseEvent SecuriteDeclenche(Me, "Aérotherme bloqué : débit insuffisant")
            End If
            PiloterRelaiMono(Relais.NomRelais.Aerotherme, aeroOK)
        End If

        ' ── 5. Notifier l'IHM ────────────────────────────────────────────────
        RaiseEvent EtatChange(Me, New EtatChronogrammeEventArgs() With {
            .PompeActive      = etape.PompeActive,
            .RechauffeurActif = etape.RechauffeurActif,
            .AerothermeActif  = etape.AerothermeActif,
            .DebitOK          = debitOK,
            .EtatsRelaisDyn   = New Dictionary(Of String, Boolean)(
                RelaisDynamiques.ToDictionary(Function(r) r.Id, Function(r) r.Etat))
        })
    End Sub

    Private Sub PiloterSortie(numeroCentrale As Integer, numeroSortie As Integer, activer As Boolean)
        ' Mode Booléen : 0V ou UMax V
        If Gestionnaire Is Nothing Then Return
        Dim c = Gestionnaire.ObtenirCentrale(numeroCentrale)
        If c Is Nothing OrElse Not c.EstConnectee Then Return
        Dim sortie = c.Voies.TrouverSortie(numeroSortie)
        Dim umax = If(sortie IsNot Nothing, sortie.UMax, 5.0)
        If activer Then
            c.Keithley.SetTension(numeroSortie, umax)
        Else
            c.Keithley.SetTension(numeroSortie, 0.0)
        End If
    End Sub

    Private Sub PiloterSortieAnalogique(numeroCentrale As Integer, numeroSortie As Integer,
                                         tensionV As Double, umaxV As Double)
        ' Tensions négatives autorisées (mode Analogique full) — la borne basse est déjà appliquée en amont
        If Gestionnaire Is Nothing Then Return
        Dim c = Gestionnaire.ObtenirCentrale(numeroCentrale)
        If c Is Nothing OrElse Not c.EstConnectee Then Return
        ' Pas de Math.Max(0) ici pour permettre les tensions négatives du mode Analogique full
        Dim t = Math.Max(-umaxV, Math.Min(umaxV, tensionV))
        c.Keithley.SetTension(numeroSortie, t)
    End Sub

    Private Sub PiloterRelaiMono(nom As Relais.NomRelais, activer As Boolean)
        If Not _relaisMono.ContainsKey(nom) Then Return
        Dim r = _relaisMono(nom)
        If r.Etat = activer Then Return
        r.Etat = activer
        If activer Then Keithley.FermerRelais(r.VoieKeithley) Else Keithley.OuvrirRelais(r.VoieKeithley)
    End Sub

    Private Sub ToutEteindre()
        For Each rd In RelaisDynamiques
            PiloterSortie(rd.NumeroCentrale, rd.NumeroSortie, False)
            rd.Etat = False
        Next
        If _relaisMono IsNot Nothing Then
            For Each kvp In _relaisMono
                PiloterRelaiMono(kvp.Key, False)
            Next
        End If
    End Sub

End Class

' ─── EventArgs ────────────────────────────────────────────────────────────────

Public Class EtatChronogrammeEventArgs
    Public Property PompeActive      As Boolean
    Public Property RechauffeurActif As Boolean
    Public Property AerothermeActif  As Boolean
    Public Property DebitOK          As Boolean
    ' États des relais dynamiques : clé = RelaisDynamique.Id
    Public Property EtatsRelaisDyn   As Dictionary(Of String, Boolean)
End Class
