Imports System
Imports System.Collections.Generic
Imports System.Linq
Imports System.Text
Imports Microsoft.VisualBasic

' ═══════════════════════════════════════════════════════════════════════════════
'  VOIE CALCULÉE — définition d'un calcul utilisateur
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Représente une voie virtuelle définie par une expression mathématique.
''' Exemples :
'''   Puissance = ({C1_V111} - {C1_V108}) * {C1_V110} * 4186 / 3600
'''   DeltaT    = {C1_V101} - {C1_V102}
'''   Rendement = ({C1_V103} - {C1_V104}) / ({C1_V101} - {C1_V104}) * 100
''' Les références de voies utilisent la notation {CLE_HISTORIQUE}.
''' </summary>
Public Class VoieCalculee

    ' ─── Identification ───────────────────────────────────────────────────────

    Public Property Id          As String = ""        ' identifiant unique (GUID court)
    Public Property Nom         As String = ""        ' nom affiché ex: "Puissance"
    Public Property Unite       As String = ""        ' unité ex: "W", "kW", "°C"
    Public Property Expression  As String = ""        ' formule ex: "({C1_V111}-{C1_V108})*{C1_V110}*4186"
    Public Property Active      As Boolean = True
    Public Property NbPointsMoyenne As Integer = 1   ' > 1 → moyenne glissante

    ' ─── État courant (non persisté) ─────────────────────────────────────────

    Public Property Valeur        As Double = Double.NaN
    Public Property EnErreur      As Boolean = False
    Public Property MessageErreur As String = ""

    ' ─── État interne intégration (non persisté) ─────────────────────────────

    Private _integrale          As Double = 0.0
    Private _tempsEcouleS       As Double = 0.0
    Private _integrationActive  As Boolean = False
    Private _integrationGelee   As Boolean = False
    Private _t1S                As Double = Double.NaN
    Private _t2S                As Double = Double.NaN
    Private _intExprBrute       As String = ""
    Private _suffixeApresInt    As String = ""
    Private _estIntegrale       As Boolean = False
    Private _valeurAvantInt     As Double = Double.NaN

    ' ─── Clé historique ───────────────────────────────────────────────────────

    Public ReadOnly Property CleHistorique As String
        Get
            Return "CALC_" & Id
        End Get
    End Property

    Public ReadOnly Property NomAffiche As String
        Get
            Return "[Calcul] " & Nom
        End Get
    End Property

    ' ─── Calcul ───────────────────────────────────────────────────────────────

    ''' <summary>
    ''' Évalue l'expression en substituant les valeurs courantes de l'historique.
    ''' Gère la moyenne glissante si NbPointsMoyenne > 1.
    ''' </summary>
    ''' <summary>Réinitialise l'accumulateur d'intégration. Appeler au démarrage acquisition.</summary>
    Public Sub ResetIntegration()
        _integrale         = 0.0
        _tempsEcouleS      = 0.0
        _integrationActive = False
        _integrationGelee  = False
        _valeurAvantInt    = Double.NaN
        _estIntegrale      = False
        _intExprBrute      = ""
        _t1S               = Double.NaN
        _t2S               = Double.NaN
        _suffixeApresInt   = ""
        AnalyserExpressionIntegrale()
    End Sub

    Private Sub AnalyserExpressionIntegrale()
        Dim m = System.Text.RegularExpressions.Regex.Match(
            Expression.Trim(),
            "^int\((.+?),([^,]*),([^)]*)\)(.*)$",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase Or
            System.Text.RegularExpressions.RegexOptions.Singleline)
        If Not m.Success Then Return
        _estIntegrale    = True
        ' Accepter les deux syntaxes : int(expr*dt,...) ou int(expr,...)
        ' Si *dt est présent dans l'expression, on le retire (int() applique déjà *dt)
        Dim exprBrute = m.Groups(1).Value.Trim()
        exprBrute = System.Text.RegularExpressions.Regex.Replace(
            exprBrute, "\s*\*\s*dt\s*$", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        exprBrute = System.Text.RegularExpressions.Regex.Replace(
            exprBrute, "\s*dt\s*\*\s*", "",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase)
        _intExprBrute    = exprBrute
        _suffixeApresInt = m.Groups(4).Value.Trim()
        Dim t1Str = m.Groups(2).Value.Trim()
        Dim t2Str = m.Groups(3).Value.Trim()
        _t1S = Double.NaN : _t2S = Double.NaN
        If t1Str <> "" Then Try : _t1S = ParseurDuree.EnSecondes(t1Str) : Catch : End Try
        If t2Str <> "" Then Try : _t2S = ParseurDuree.EnSecondes(t2Str) : Catch : End Try
        _integrationActive = Double.IsNaN(_t1S)
    End Sub

    ''' <summary>
    ''' Évalue l'expression. dt = pas d'acquisition en secondes.
    ''' Supporte int(expr*dt, t1, t2) pour l'intégration incrémentale.
    ''' </summary>
    Public Sub Calculer(historique As HistoriqueMultiCentrale, Optional dt As Double = 5.0)
        If String.IsNullOrWhiteSpace(Expression) Then
            Valeur = Double.NaN : EnErreur = True
            MessageErreur = "Expression vide" : Return
        End If
        ' Analyser l'expression au premier appel (si ResetIntegration n'a pas été appelé)
        If Not _estIntegrale AndAlso
           Expression.TrimStart().StartsWith("int(", StringComparison.OrdinalIgnoreCase) Then
            AnalyserExpressionIntegrale()
        End If
        Try
            If _estIntegrale Then
                CalculerIntegrale(historique, dt)
            Else
                CalculerStandard(historique, dt)
            End If
        Catch ex As Exception
            Valeur = Double.NaN : EnErreur = True : MessageErreur = ex.Message
        End Try
    End Sub

    Private Sub CalculerStandard(historique As HistoriqueMultiCentrale, dt As Double)
        Dim valeurs As New Dictionary(Of String, Double)()
        Dim refs = ExtraireRefs(Expression)
        If NbPointsMoyenne <= 1 Then
            For Each ref In refs
                Dim serie = historique.ObtenirSerie(ref)
                If serie Is Nothing OrElse serie.Count = 0 Then
                    Throw New Exception("Voie introuvable : " & ref)
                End If
                Dim dernier = serie.Last()
                If dernier.EnErreur OrElse Double.IsNaN(dernier.Valeur) Then
                    Throw New Exception("Voie en erreur : " & ref)
                End If
                valeurs(ref) = dernier.Valeur
            Next
        Else
            For Each ref In refs
                Dim serie = historique.ObtenirSerie(ref)
                If serie Is Nothing OrElse serie.Count = 0 Then
                    Throw New Exception("Voie introuvable : " & ref)
                End If
                Dim nbPts  = Math.Min(NbPointsMoyenne, serie.Count)
                Dim points = serie.Skip(serie.Count - nbPts) _
                                  .Where(Function(p) Not p.EnErreur AndAlso Not Double.IsNaN(p.Valeur)) _
                                  .ToList()
                If points.Count = 0 Then Throw New Exception("Pas de points valides : " & ref)
                valeurs(ref) = points.Average(Function(p) p.Valeur)
            Next
        End If
        Dim exprNum = SubstituerRefs(Expression, valeurs, dt)
        Valeur = EvaluateurExpression.Evaluer(exprNum)
        EnErreur = False : MessageErreur = ""
    End Sub

    Private Sub CalculerIntegrale(historique As HistoriqueMultiCentrale, dt As Double)
        If _integrationGelee Then
            Valeur = AppliquerSuffixe(_integrale) : EnErreur = False : Return
        End If
        ' Évaluer l'expression interne
        Dim refs    = ExtraireRefs(_intExprBrute)
        Dim valeurs = New Dictionary(Of String, Double)()
        For Each ref In refs
            Dim serie = historique.ObtenirSerie(ref)
            If serie Is Nothing OrElse serie.Count = 0 Then
                Throw New Exception("Voie introuvable dans int() : " & ref)
            End If
            valeurs(ref) = serie.Last().Valeur
        Next
        ' Ne PAS substituer dt dans l'expression interne — c'est int() qui applique *dt
        Dim exprNum     = SubstituerRefs(_intExprBrute, valeurs, 1.0)
        Dim valCourante = EvaluateurExpression.Evaluer(exprNum)
        Dim tActuel    = _tempsEcouleS

        If Not _integrationActive Then
            If Not Double.IsNaN(_t1S) AndAlso tActuel + dt > _t1S Then
                Dim fracApres = (tActuel + dt) - _t1S
                Dim valAt1    = If(Double.IsNaN(_valeurAvantInt), valCourante,
                    _valeurAvantInt + (valCourante - _valeurAvantInt) * ((tActuel + dt - _t1S) / dt))
                _integrale += (valAt1 + valCourante) / 2.0 * fracApres
                _integrationActive = True
            End If
        Else
            If Not Double.IsNaN(_t2S) AndAlso tActuel >= _t2S Then
                _integrationGelee = True
            ElseIf Not Double.IsNaN(_t2S) AndAlso tActuel + dt > _t2S Then
                Dim fracDans = _t2S - tActuel
                Dim valAt2   = If(Double.IsNaN(_valeurAvantInt), valCourante,
                    _valeurAvantInt + (valCourante - _valeurAvantInt) * (fracDans / dt))
                _integrale += (If(Double.IsNaN(_valeurAvantInt), valCourante, _valeurAvantInt) + valAt2) / 2.0 * fracDans
                _integrationGelee = True
            Else
                If Double.IsNaN(_valeurAvantInt) Then
                    ' Premier point : rectangle (pas de valeur précédente)
                    _integrale += valCourante * dt
                Else
                    _integrale += (_valeurAvantInt + valCourante) / 2.0 * dt
                End If
            End If
        End If
        _valeurAvantInt = valCourante
        _tempsEcouleS  += dt
        Valeur           = AppliquerSuffixe(_integrale)
        EnErreur         = False : MessageErreur = ""
    End Sub

    Private Function AppliquerSuffixe(v As Double) As Double
        If _suffixeApresInt = "" Then Return v
        Try
            Return EvaluateurExpression.Evaluer(
                v.ToString("G17", System.Globalization.CultureInfo.InvariantCulture) &
                _suffixeApresInt)
        Catch
            Return v
        End Try
    End Function

    ''' <summary>Extrait toutes les références {CLE} de l'expression.</summary>
    Public Shared Function ExtraireRefs(expression As String) As List(Of String)
        Dim refs As New List(Of String)()
        Dim i = 0
        Do While i < expression.Length
            If expression(i) = "{"c Then
                Dim j = expression.IndexOf("}"c, i)
                If j > i Then
                    Dim ref = expression.Substring(i + 1, j - i - 1).Trim()
                    If Not refs.Contains(ref) Then refs.Add(ref)
                    i = j + 1
                    Continue Do
                End If
            End If
            i += 1
        Loop
        Return refs
    End Function

    Private Shared Function SubstituerRefs(expression As String,
                                            valeurs As Dictionary(Of String, Double),
                                            Optional dt As Double = 5.0) As String
        Dim sb As New StringBuilder(expression)
        For Each kvp In valeurs
            Dim valStr = kvp.Value.ToString("G15", System.Globalization.CultureInfo.InvariantCulture)
            sb.Replace("{" & kvp.Key & "}", valStr)
        Next
        ' Substituer la variable spéciale dt
        Dim dtStr = dt.ToString("G17", System.Globalization.CultureInfo.InvariantCulture)
        sb.Replace("dt", dtStr)
        Return sb.ToString()
    End Function

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Public Sub SauverDansConfig(cfg As ConfigManager, section As String, index As Integer)
        Dim pref = "Calc" & index & "_"
        cfg.Set_(section, pref & "Id",         Id)
        cfg.Set_(section, pref & "Nom",        Nom)
        cfg.Set_(section, pref & "Unite",      Unite)
        cfg.Set_(section, pref & "Expression", Expression)
        cfg.Set_(section, pref & "Active",     Active)
        cfg.Set_(section, pref & "NbMoy",      NbPointsMoyenne)
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager, section As String, index As Integer)
        Dim pref = "Calc" & index & "_"
        Id               = cfg.Get_(section, pref & "Id",         "")
        Nom              = cfg.Get_(section, pref & "Nom",        "")
        Unite            = cfg.Get_(section, pref & "Unite",      "")
        Expression       = cfg.Get_(section, pref & "Expression", "")
        Active           = cfg.GetBool(section, pref & "Active",  True)
        NbPointsMoyenne  = cfg.GetInt(section,  pref & "NbMoy",   1)
    End Sub

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  GESTIONNAIRE DE VOIES CALCULÉES
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Gère la liste de toutes les voies calculées.
''' Calcule et injecte les résultats dans l'historique à chaque acquisition.
''' </summary>
Public Class GestionnaireCalculs

    Private Const SEC As String = "Calculs"

    Public ReadOnly Voies As New List(Of VoieCalculee)()

    ' ─── Calcul à chaque acquisition ──────────────────────────────────────────

    ''' <summary>
    ''' Évalue toutes les voies actives et injecte les résultats dans l'historique.
    ''' À appeler après chaque AjouterMesuresCentrale.
    ''' </summary>
    ''' <summary>dt = pas d'acquisition en secondes.</summary>
    Public Sub CalculerEtInjecter(historique As HistoriqueMultiCentrale,
                                  horodatage As DateTime,
                                  Optional dt As Double = 5.0)
        For Each vc In Voies.Where(Function(v) v.Active)
            vc.Calculer(historique, dt)
            Dim pt As New PointMesure() With {
                .Horodatage = horodatage,
                .Valeur     = vc.Valeur,
                .EnErreur   = vc.EnErreur
            }
            historique.InjecterPoint(vc.CleHistorique, pt)
        Next
    End Sub

    ''' <summary>Réinitialise tous les accumulateurs d'intégration. Appeler au démarrage.</summary>
    Public Sub ResetIntegrations()
        For Each vc In Voies
            vc.ResetIntegration()
        Next
    End Sub

    ' ─── Entête / ligne CSV ───────────────────────────────────────────────────

    Public Function EnteteCSV(separateur As String) As String
        Return String.Join(separateur,
            Voies.Where(Function(v) v.Active) _
                 .Select(Function(v) v.NomAffiche & " (" & v.Unite & ")"))
    End Function

    Public Function LigneCSV(separateur As String, nbDec As Integer) As String
        Return String.Join(separateur,
            Voies.Where(Function(v) v.Active) _
                 .Select(Function(v)
                     If v.EnErreur OrElse Double.IsNaN(v.Valeur) Then Return "ERR"
                     Return v.Valeur.ToString("F" & nbDec,
                         System.Globalization.CultureInfo.InvariantCulture)
                 End Function))
    End Function

    ' ─── Persistance ──────────────────────────────────────────────────────────

    Public Sub SauverDansConfig(cfg As ConfigManager)
        cfg.Set_(SEC, "NbCalculs", Voies.Count)
        For i As Integer = 0 To Voies.Count - 1
            Voies(i).SauverDansConfig(cfg, SEC, i)
        Next
    End Sub

    Public Sub ChargerDepuisConfig(cfg As ConfigManager)
        Voies.Clear()
        Dim nb = cfg.GetInt(SEC, "NbCalculs", 0)
        For i As Integer = 0 To nb - 1
            Dim vc As New VoieCalculee()
            vc.ChargerDepuisConfig(cfg, SEC, i)
            If vc.Id = "" Then vc.Id = NouvelId()
            Voies.Add(vc)
        Next
    End Sub

    Private Shared _compteurId As Integer = 0
    Public Shared Function NouvelId() As String
        ' Compteur incrémental + timestamp pour garantir l'unicité absolue
        System.Threading.Interlocked.Increment(_compteurId)
        Return "V" & DateTime.Now.ToString("yyMMddHHmmss") & _compteurId.ToString("D4")
    End Function

End Class

' ═══════════════════════════════════════════════════════════════════════════════
'  ÉVALUATEUR D'EXPRESSIONS MATHÉMATIQUES
' ═══════════════════════════════════════════════════════════════════════════════

''' <summary>
''' Parseur récursif descendant pour expressions mathématiques.
''' Opérateurs : + - * / ^ (puissance)
''' Fonctions   : abs, sqrt, ln, log, log10, exp, sin, cos, tan, asin, acos, atan,
'''               min(a,b), max(a,b), floor, ceil, round
''' Constantes  : pi, e
''' Priorité    : () > fonctions > ^ > * / > + -
''' </summary>
Public Class EvaluateurExpression

    Private _expr  As String
    Private _pos   As Integer

    Private Sub New(expression As String)
        ' Normaliser : supprimer espaces, virgule décimale → point
        _expr = expression.Replace(" ", "").Replace(",", ".")
        _pos  = 0
    End Sub

    ''' <summary>Point d'entrée principal.</summary>
    Public Shared Function Evaluer(expression As String) As Double
        If String.IsNullOrWhiteSpace(expression) Then
            Throw New ArgumentException("Expression vide")
        End If
        Dim eval As New EvaluateurExpression(expression)
        Dim resultat = eval.ParseAddSub()
        If eval._pos < eval._expr.Length Then
            Throw New FormatException("Caractère inattendu à la position " & eval._pos)
        End If
        Return resultat
    End Function

    ' ─── Grammaire récursive ──────────────────────────────────────────────────

    Private Function ParseAddSub() As Double
        Dim gauche = ParseMulDiv()
        Do While _pos < _expr.Length
            Dim op = _expr(_pos)
            If op <> "+"c AndAlso op <> "-"c Then Exit Do
            _pos += 1
            Dim droite = ParseMulDiv()
            gauche = If(op = "+"c, gauche + droite, gauche - droite)
        Loop
        Return gauche
    End Function

    Private Function ParseMulDiv() As Double
        Dim gauche = ParsePuissance()
        Do While _pos < _expr.Length
            Dim op = _expr(_pos)
            If op <> "*"c AndAlso op <> "/"c Then Exit Do
            _pos += 1
            Dim droite = ParsePuissance()
            If op = "/"c AndAlso droite = 0 Then
                Throw New DivideByZeroException("Division par zéro")
            End If
            gauche = If(op = "*"c, gauche * droite, gauche / droite)
        Loop
        Return gauche
    End Function

    Private Function ParsePuissance() As Double
        Dim base = ParseUnaire()
        If _pos < _expr.Length AndAlso _expr(_pos) = "^"c Then
            _pos += 1
            Dim exp = ParseUnaire()   ' associativité droite
            Return Math.Pow(base, exp)
        End If
        Return base
    End Function

    Private Function ParseUnaire() As Double
        If _pos < _expr.Length AndAlso _expr(_pos) = "-"c Then
            _pos += 1
            Return -ParsePrimaire()
        End If
        If _pos < _expr.Length AndAlso _expr(_pos) = "+"c Then
            _pos += 1
        End If
        Return ParsePrimaire()
    End Function

    Private Function ParsePrimaire() As Double
        If _pos >= _expr.Length Then Throw New FormatException("Expression incomplète")

        ' Parenthèse
        If _expr(_pos) = "("c Then
            _pos += 1
            Dim v = ParseAddSub()
            If _pos >= _expr.Length OrElse _expr(_pos) <> ")"c Then
                Throw New FormatException("Parenthèse fermante manquante")
            End If
            _pos += 1
            Return v
        End If

        ' Nombre
        If Char.IsDigit(_expr(_pos)) OrElse _expr(_pos) = "."c Then
            Return LireNombre()
        End If

        ' Identifiant (fonction ou constante)
        If Char.IsLetter(_expr(_pos)) Then
            Return LireFonctionOuConstante()
        End If

        Throw New FormatException("Caractère inattendu : '" & _expr(_pos) & "' à la position " & _pos)
    End Function

    ' ─── Lecture ──────────────────────────────────────────────────────────────

    Private Function LireNombre() As Double
        Dim debut = _pos
        Do While _pos < _expr.Length AndAlso
                 (Char.IsDigit(_expr(_pos)) OrElse _expr(_pos) = "."c OrElse
                  _expr(_pos) = "e"c OrElse _expr(_pos) = "E"c OrElse
                  ((_expr(_pos) = "+"c OrElse _expr(_pos) = "-"c) AndAlso
                   _pos > 0 AndAlso (_expr(_pos - 1) = "e"c OrElse _expr(_pos - 1) = "E"c)))
            _pos += 1
        Loop
        Dim s = _expr.Substring(debut, _pos - debut)
        Dim v As Double
        If Not Double.TryParse(s, System.Globalization.NumberStyles.Float,
                               System.Globalization.CultureInfo.InvariantCulture, v) Then
            Throw New FormatException("Nombre invalide : " & s)
        End If
        Return v
    End Function

    Private Function LireFonctionOuConstante() As Double
        Dim debut = _pos
        Do While _pos < _expr.Length AndAlso (Char.IsLetterOrDigit(_expr(_pos)) OrElse _expr(_pos) = "_"c)
            _pos += 1
        Loop
        Dim nom = _expr.Substring(debut, _pos - debut).ToLowerInvariant()

        ' Constantes
        Select Case nom
            Case "pi"  : Return Math.PI
            Case "e"   : Return Math.E
        End Select

        ' Fonctions à 1 argument
        Dim fonctionsSimples As New Dictionary(Of String, Func(Of Double, Double)) From {
            {"abs",   AddressOf Math.Abs},
            {"sqrt",  AddressOf Math.Sqrt},
            {"ln",    AddressOf Math.Log},
            {"log",   AddressOf Math.Log10},
            {"log10", AddressOf Math.Log10},
            {"exp",   AddressOf Math.Exp},
            {"sin",   AddressOf Math.Sin},
            {"cos",   AddressOf Math.Cos},
            {"tan",   AddressOf Math.Tan},
            {"asin",  AddressOf Math.Asin},
            {"acos",  AddressOf Math.Acos},
            {"atan",  AddressOf Math.Atan},
            {"floor", AddressOf Math.Floor},
            {"ceil",  AddressOf Math.Ceiling},
            {"round", AddressOf Math.Round}
        }

        If fonctionsSimples.ContainsKey(nom) Then
            If _pos >= _expr.Length OrElse _expr(_pos) <> "("c Then
                Throw New FormatException("'(' attendu après " & nom)
            End If
            _pos += 1
            Dim arg = ParseAddSub()
            If _pos >= _expr.Length OrElse _expr(_pos) <> ")"c Then
                Throw New FormatException("')' attendu après argument de " & nom)
            End If
            _pos += 1
            Return fonctionsSimples(nom)(arg)
        End If

        ' Fonctions à 2 arguments
        If nom = "min" OrElse nom = "max" OrElse nom = "atan2" OrElse nom = "pow" Then
            If _pos >= _expr.Length OrElse _expr(_pos) <> "("c Then
                Throw New FormatException("'(' attendu après " & nom)
            End If
            _pos += 1
            Dim a = ParseAddSub()
            If _pos >= _expr.Length OrElse _expr(_pos) <> ","c Then
                Throw New FormatException("',' attendu dans " & nom)
            End If
            _pos += 1
            Dim b = ParseAddSub()
            If _pos >= _expr.Length OrElse _expr(_pos) <> ")"c Then
                Throw New FormatException("')' attendu après 2e argument de " & nom)
            End If
            _pos += 1
            Select Case nom
                Case "min"   : Return Math.Min(a, b)
                Case "max"   : Return Math.Max(a, b)
                Case "atan2" : Return Math.Atan2(a, b)
                Case "pow"   : Return Math.Pow(a, b)
            End Select
        End If

        Throw New FormatException("Fonction inconnue : " & nom)
    End Function

End Class
