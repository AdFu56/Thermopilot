Imports System
Imports System.Collections.Generic
Imports System.Net.Sockets
Imports System.Text
Imports System.Threading
Imports Microsoft.VisualBasic

Public Class KeithleyComm
    Implements IDisposable

    Private _client As TcpClient
    Private _stream As NetworkStream
    Private _lock As New Object()

    ' Propriétés pour la compatibilité avec l'interface
    Public Property IPAddress As String = "192.168.0.3"
    Public Property Port As Integer = 1394
    Public Property IsConnected As Boolean = False

    ' On garde ces propriétés pour éviter les erreurs BC30456 dans OngletConnexion
    ' même si elles ne sont plus utilisées pour le TCP
    Public Property PortName As String = ""
    Public Property BaudRate As Integer = 9600
    Public Property ReadTimeout As Integer = 3000

    Public Function Connect() As Boolean
        Try
            If IsConnected Then Disconnect()
            _client = New TcpClient()
            Dim result = _client.BeginConnect(IPAddress, Port, Nothing, Nothing)
            Dim success = result.AsyncWaitHandle.WaitOne(ReadTimeout)

            If Not success Then Return False

            _client.EndConnect(result)
            _stream = _client.GetStream()
            _stream.ReadTimeout = ReadTimeout
            IsConnected = True

            ' Initialisation standard Keithley
            SendCommand("*RST")
            Return True
        Catch
            Return False
        End Try
    End Function

    Public Sub SendCommand(cmd As String)
        If Not IsConnected OrElse _stream Is Nothing Then Return
        SyncLock _lock
            SendRaw(cmd)
        End SyncLock
    End Sub

    ' Envoi brut sans lock — utilisé en interne par Query qui locke déjà
    Private Sub SendRaw(cmd As String)
        Try
            Dim data As Byte() = Encoding.ASCII.GetBytes(cmd & vbCr)
            _stream.Write(data, 0, data.Length)
        Catch
            IsConnected = False
        End Try
    End Sub

    ' Délai en ms entre l'envoi d'une commande et la lecture de la réponse.
    ' Augmenter si ERR apparaît sur les voies à partir d'un certain rang.
    Public Property DelaiLectureMs As Integer = 100

    Public Function Query(cmd As String) As String
        If Not IsConnected OrElse _stream Is Nothing Then Return ""
        SyncLock _lock
            Try
                SendRaw(cmd)

                ' Attendre que la centrale ait le temps de préparer la réponse
                If DelaiLectureMs > 0 Then Thread.Sleep(DelaiLectureMs)

                ' Lecture robuste multi-paquets :
                ' Le Keithley peut envoyer la réponse en plusieurs fragments TCP.
                ' On lit en boucle jusqu'à ce que les données soient stables.
                Dim sb As New StringBuilder()
                Dim buf(4095) As Byte
                Dim deadline = DateTime.Now.AddMilliseconds(_stream.ReadTimeout)

                Do
                    If _stream.DataAvailable Then
                        Dim n = _stream.Read(buf, 0, buf.Length)
                        If n > 0 Then sb.Append(Encoding.ASCII.GetString(buf, 0, n))
                    Else
                        ' Pas de données disponibles — si on a déjà quelque chose, on attend
                        ' brièvement pour voir si un autre paquet arrive
                        If sb.Length > 0 Then
                            Thread.Sleep(20)
                            If Not _stream.DataAvailable Then Exit Do
                        Else
                            Thread.Sleep(10)
                        End If
                    End If
                    If DateTime.Now > deadline Then Exit Do
                Loop

                Return sb.ToString().Trim()
            Catch
                Return ""
            End Try
        End SyncLock
    End Function

    Public Sub ConfigurerScan(voiesTemp As String, voiesDebit As String, typeTC As String)
        ' Séquence calquée sur l'ancien KI2701config() fonctionnel

        SendCommand("*RST")
        SendCommand("FORM:ELEM READ")   ' Format : valeurs uniquement
        SendCommand("TRAC:CLE")         ' Vider le buffer

        ' Configuration des thermocouples
        If Not String.IsNullOrEmpty(voiesTemp) Then
            SendCommand(String.Format("UNIT:TEMP C,(@{0})", voiesTemp))
            SendCommand(String.Format("FUNC 'TEMP',(@{0})", voiesTemp))
            SendCommand(String.Format("TEMP:TRAN TC,(@{0})", voiesTemp))
            SendCommand(String.Format("TEMP:TC:TYPE {0},(@{1})", typeTC, voiesTemp))
            SendCommand(String.Format("SENS:TEMP:APER 0.05,(@{0})", voiesTemp))
        End If

        ' Configuration des voies tension DC (débitmètres, capteurs 4-20mA...)
        If Not String.IsNullOrEmpty(voiesDebit) Then
            SendCommand(String.Format("FUNC 'VOLT:DC',(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:APER 0.05,(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:RANG:AUTO ON,(@{0})", voiesDebit))
            SendCommand(String.Format("VOLT:DIG 6,(@{0})", voiesDebit))
        End If

        ' Construire la liste de scan
        Dim liste As String
        If Not String.IsNullOrEmpty(voiesTemp) AndAlso Not String.IsNullOrEmpty(voiesDebit) Then
            liste = voiesTemp & "," & voiesDebit
        Else
            liste = If(String.IsNullOrEmpty(voiesTemp), voiesDebit, voiesTemp)
        End If

        ' Compter le nombre de voies
        Dim nbVoies = liste.Split(","c).Length

        ' Séquence de déclenchement identique à l'ancien code
        SendCommand("INIT:CONT OFF")                                    ' Désactiver initiation continue
        SendCommand("TRIG:COUN 1")                                      ' 1 scan par déclenchement
        SendCommand(String.Format("SAMP:COUN {0}", nbVoies))            ' Nombre de voies
        SendCommand(String.Format("ROUT:SCAN (@{0})", liste))           ' Liste de scan
        SendCommand("ROUT:SCAN:TSO IMM")                                ' Démarrage immédiat
        SendCommand("ROUT:SCAN:LSEL INT")                               ' Activer le scan interne
    End Sub

    Public Sub ConfigurerRelais(voies As List(Of Integer))
        SendCommand("ROUT:OPEN:ALL")
    End Sub

    Public Function LireScan() As String
        ' Calqué sur l'ancien : send("Read?") puis KI2701Read()
        Return Query("Read?")
    End Function

    Public Sub FermerRelais(voie As Integer)
        ' Sortie booléenne ON : tension fixe 5V (compatible ancien code)
        SendCommand(String.Format("OUTP:VOLT 5.0, (@{0})", voie))
    End Sub

    Public Sub OuvrirRelais(voie As Integer)
        ' Sortie booléenne OFF : tension 0V
        SendCommand(String.Format("OUTP:VOLT 0.0, (@{0})", voie))
    End Sub

    ''' <summary>
    ''' Pilote une sortie analogique à une tension précise.
    ''' Utilisé en mode Analogique (0–UMax V).
    ''' </summary>
    Public Sub SetTension(voie As Integer, tensionV As Double)
        SendCommand(String.Format("OUTP:VOLT {0}, (@{1})",
            tensionV.ToString("F3", System.Globalization.CultureInfo.InvariantCulture),
            voie))
    End Sub

    Public Sub Disconnect()
        Try
            If _stream IsNot Nothing Then _stream.Close()
            If _client IsNot Nothing Then _client.Close()
        Catch
        End Try
        IsConnected = False
    End Sub

    Public Sub Dispose() Implements IDisposable.Dispose
        Disconnect()
    End Sub
End Class