{**********************************************************************
    $Id: imapcopymain.pas,v 1.00
    This file is part of imapcopy
    Copyright (c) 2001-2009 Armin Diehl <ad@ardiehl.de>

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.
 **********************************************************************}

{ ad 2001/10/08: Problem if copying INBOX and Source and Dest are supporting
                 Namespace -> INBOX.INBOX was used
                 accept Cfg-File also if lower case (for Linux)
  ad 2001/10/09: Again INBOX.INBOX if Source does not support NAMESPACE
  ad 2002/01/26: while fetching the folder list, lotus notes (5.0.8 linux)
                 returns length and the folder name in the next line. That
                 was not supported
                 Notes somtimes returns invalid INTERNALDATES, seems to be that
                 notes does not check dates ;-), now we check the date and in
                 case we got a wrong internaldate, we fetch the rfc822 Date:
                 now
  ad 2003/07/15: minor changes for compiling with FreePascal 1.0.10 and 1.1
  ad 2003/09/12: a last blank line in the config file is no longer needed
                 AllowFlags in config file to remove message flags that are
                 not supported by the destination server
                 Support Tabs in config file
                 Support -e to create empty folders
                 Added -0 to create folders only
                 Added -s to subscribe folders
                 Added -u to subscribe Src-Folder if Select fails
  ad 2003/09/12: -e was only possible via config file switch CreateEmptyFolders
  ad 2003/11/21: in case the server returns internaldate with a one digit day,
                 insert a 0 at the first position
  ad 2003/12/04: Path Separator \\ was not processed as \
  ad 2003/12/14: More Problems with Path Seperator (for Notes)
                 Fixed getting Folder List for Notes (Folders with Blanks)
  ad 2004/03/31: Added option to copy all messages to INBOX
                 Fixed timeout problem in APPEND command
  ad 2004/05/09: now using ReadALineAndCount to count received bytes while
                 fetching message body. Previous versions assumed that lines
                 within messages are always terminated by CRLF. That was
                 nonsense (Thanks Ciprian Vizitiu for figuring that out)
  ad 2005/02/25: Applied patch from Christiaan den Besten to supress
                 INBOX.INBOX and Skip match SrcFolder (-M)
  ad 2005/04/10: Fixed memory leak
  ad 2006/04/20: Add DenyFlags directive in config file to filter out i.e. the \Recent flag
                 - Sorry Ciprian for the trouble you had with this
  ad 2009/07/18: added optional conversion of timezones used in append command (see ImapCopy.cfg)
}

UNIT imapcopymain;

{$IFDEF FPC}
  {$MODE Delphi}
{$ENDIF}

INTERFACE

{$I-}

USES SysUtils, Classes, ImapTools,
     {$IFDEF Win32}
     WinSock,
     {$ELSE}
       {$IFDEF Unix}
       //Sockets,                // for Linux and FreeBSD
       {$ELSE}
       Error, Unknown Opsys
       {$ENDIF}
     {$ENDIF}
     GetOpts, logfile;

CONST VersionK = '1.04 - 2009/07/18';

PROCEDURE ParseCommandLine;
PROCEDURE Header;
PROCEDURE Main;

IMPLEMENTATION

CONST CfgFileK  = 'ImapCopy.cfg';
      {$IFDEF UNIX}
      CfgFile2K = 'imapcopy.cfg';                // for Unix
      {$ENDIF}

VAR SrcThisFoldersOnly,
    SrcSkipFolders        : STRING;              // List of folders separated by ,
    SrcMatchFolders       : TStringList = nil;
    OnlyOneMessage        : BOOLEAN = FALSE;     // copy only the first message of each folder
    CreateEmptyFolders    : BOOLEAN = FALSE;     // Create Empty Folders also
    CopyFoldersOnly       : BOOLEAN = FALSE;
    TestMode              : BOOLEAN = FALSE;     // test login for all users
    SubscribeFolder       : BOOLEAN = FALSE;
    SubscribeSrcFolder    : BOOLEAN = FALSE;
    CopyAllMessagesToInbox: BOOLEAN = FALSE;
    AllowFlags            : STRING = '';
    AllowFlagsList        : TStringList;
    DenyFlags             : STRING = '';
    DenyFlagsList         : TStringList;
    InfoOnly              : BOOLEAN;
    TotalNumMsgs          : INTEGER = 0;
    TotalErrs             : INTEGER = 0;
    TotalUsers            : INTEGER = 0;
    TotalFoldersCreated   : INTEGER = 0;
    TotalFolderCreateErrs : INTEGER = 0;
    TotalFoldersNotCopieed: INTEGER = 0;
    DstRootFolder         : STRING = '';


PROCEDURE Header;
VAR S : STRING;
BEGIN
  S := 'IMAPCopy '+VersionK;
  {$IFDEF FPC}
  S := S + ' [compiled with FreePascal]';
  {$ELSE}
  S := S + ' [compiled with Delphi]';
  {$ENDIF}
  writeln (S);
  writeln ('written 2001-2009 by Armin Diehl <ad@ardiehl.de>');
  {$IFDEF Linux}
  WriteLn ('Running on Linux');
  {$ENDIF}
  {$IFDEF Win32}
  WriteLn ('Running on Win32');
  {$ENDIF}
  WriteLn;
  WriteLn ('This program is free software; you can redistribute it and/or');
  WriteLn ('modify it under the terms of the GNU General Public License');
  WriteLn ('as published by the Free Software Foundation; either version 2');
  WriteLn ('of the License, or (at your option) any later version.');
  WriteLn;
  WriteLn ('This program is distributed in the hope that it will be useful,');
  WriteLn ('but WITHOUT ANY WARRANTY; without even the implied warranty of');
  WriteLn ('MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the');
  WriteLn ('GNU General Public License for more details.');
  WriteLn;
  WriteLn ('You should have received a copy of the GNU General Public License');
  WriteLn ('along with this program; if not, write to the Free Software');
  WriteLn ('Foundation, Inc., 59 Temple Place - Suite 330, Boston, MA  02111-1307, USA.');
  WriteLn ('---------------------------------------------------------------------------');

END;

PROCEDURE Help;
BEGIN
  WriteLn ('Usage');
  WriteLn ('ImapCopy [-h] [-i] [-O SrcFolder] [-S SrcFolder] [-1] [-0] [-s] [-u]');
  WriteLn (' -O SrcFolder : Copy only SrcFolder, -O can be specified more than once');
  WriteLn (' -M SrcFolder : Skip match SrcFolder, -M can be specified more than once');
  WriteLn (' -S SrcFolder : Skip SrcFolder, -S can be specified more than once');
  WriteLn (' -i           : Show info about servers and exit without copying');
  WriteLn (' -1           : Copy only the first message of each folder');
  WriteLn (' -0           : Copy folders only, dont copy any messages');
  WriteLn (' -e           : Create emtpy folders also');
  WriteLn (' -t           : Test login for all users in config file');
  WriteLn (' -s           : Subscribe all folders on destination server');
  WriteLn (' -u           : Subscribe all folders on Source, needed by some servers to');
  WriteLn ('                select a folder (only if select failes)');
  WriteLn (' -X           : Copy all Messages to INBOX, dont create Folders on Destination');
  Writeln (' -l           : Write results to logfile imapcopy.log');
  HALT(1);
END;


FUNCTION CfgNextWord (VAR S : STRING) : STRING;
VAR P : INTEGER;
BEGIN
  DelGarbage (S);
  P := POS (#9, S);
  WHILE P > 0 DO
  BEGIN
    S[P] := ' ';
    P := POS (#9, S);
  END;
  IF Length (S) = 0 THEN
  BEGIN
    Result := S;
    EXIT;
  END;
  IF S[1] = '"' THEN
  BEGIN
    P := 2;
    WHILE (P <= Length (S)) AND (S[P] <> '"') DO
      INC (P);
    Result := Copy (S,2,P-2);
    Delete (S,1,P);
  END ELSE
  BEGIN
    P := Pos (' ', S);
    IF P = 0 THEN
    BEGIN
      Result := S;
      S := '';
    END ELSE
    BEGIN
      Result := Copy (S,1,P-1);
      Delete (S,1,P);
    END;
  END;
END;


PROCEDURE ParseCommandLine;
VAR C : CHAR;
BEGIN
  InfoOnly := FALSE;
  SrcSkipFolders := '';
  SrcThisFoldersOnly := '';
  REPEAT
    C := GetOpt ('h?HO:S:i1t0sueXl');
    CASE C OF
      '?','h','H' : Help;
      'S' : SrcSkipFolders := SrcSkipFolders + OptArg + ',';
      'M' : BEGIN
              IF SrcMatchFolders = nil then
                SrcMatchFolders := TStringList.Create;
              SrcMatchFolders.Add (OptArg);
	    END;
      'O' : SrcThisFoldersOnly := SrcThisFoldersOnly + OptArg + ',';
      'i' : InfoOnly := TRUE;
      '1' : OnlyOneMessage := TRUE;
      'e' : CreateEmptyFolders := TRUE;
      't' : TestMode := TRUE;
      '0' : CopyFoldersOnly := TRUE;
      's' : SubscribeFolder := TRUE;
      'u' : SubscribeSrcFolder := TRUE;
      'X' : CopyAllMessagesToInbox := TRUE;
      'l' : begin
              logfile.log_to_file := TRUE;
              if not logfile.log_openfile ('') then
              begin
                writeln (paramstr(0),': unable to open logfile');
                halt(2);
              end;
            end;
    END;
  UNTIL C = EndOfOptions;
END;


// Dont know if spaces between flags are always present, support both
FUNCTION Flags2StringList (Flags : STRING) : TStringList;
VAR P : PCHAR;
    F : STRING;
BEGIN
  Result := TStringList.Create;
  P := PChar (Flags);
  F := '';
  WHILE P^ <> #0 DO
  BEGIN
    IF ((P^ = '\') OR (P^ = ' ')) AND (F <> '') THEN
    BEGIN
      F := Trim (F);
      IF F <> '' THEN
        Result.Add (Trim(F));
      F := '';
    END;
    F := F + P^;
    INC (P);
  END;
  IF F <> '' THEN
    Result.Add (Trim(F));
END;

PROCEDURE RemoveUnsupportedFlags (VAR Flags : STRING);
VAR F : TStringList;
    I : INTEGER;
BEGIN
  IF AllowFlags <> '' THEN
    IF Flags <> '' THEN
    BEGIN
      IF AllowFlagsList = NIL THEN
        AllowFlagsList := Flags2StringList (AllowFlags);
      F := Flags2StringList (Flags);
      Flags := '';
      IF F.Count > 0 THEN
      BEGIN
        FOR I := 0 TO F.Count-1 DO
          IF AllowFlagsList.IndexOf(F[I]) >= 0 THEN
            Flags := Flags + F[I] + ' ';
        Flags := Trim (Flags);
      END;
    END;
  IF DenyFlags <> '' THEN
    IF Flags <> '' THEN
    BEGIN
      IF DenyFlagsList = NIL THEN
        DenyFlagsList := Flags2StringList (DenyFlags);
      F := Flags2StringList (Flags);
      Flags := '';
      IF F.Count > 0 THEN
      BEGIN
        FOR I := 0 TO F.Count-1 DO
          IF DenyFlagsList.IndexOf(F[I]) < 0 THEN
            Flags := Flags + F[I] + ' ';
        Flags := Trim (Flags);
      END;
    END;
END;

PROCEDURE Main;
VAR Src,Dst : ImapServer;
    Cmd,Line,SrcServer,DstServer: STRING;
    SrcPort,DstPort : WORD;
    T : TEXT;
    Err : INTEGER;
    SrcUser, DstUser, SrcPasswd, DstPasswd, S,S2 : STRING;
    I : INTEGER;
    DebugCommandsSrc : BOOLEAN;
    DebugCommandsDst : BOOLEAN;
    NumUserMessages,NumUserErrs : integer;

    FUNCTION FolderCopyCheck (SrcFolder : STRING) : BOOLEAN;
    VAR I : INTEGER;
    BEGIN
      Result := FALSE;
      IF SrcThisFoldersOnly <> '' THEN
        IF Pos (SrcFolder+',',SrcThisFoldersOnly) = 0 THEN
          EXIT;
      IF SrcSkipFolders <> '' THEN
        IF Pos (SrcFolder+',',SrcSkipFolders) <> 0 THEN
          EXIT;
      IF SrcMatchFolders <> nil THEN
        FOR I := 0 TO SrcMatchFolders.Count-1 DO
          IF Pos (SrcMatchFolders [I], SrcFolder) = 1 THEN
            EXIT;
      Result := TRUE;
    END;

    PROCEDURE CopyAllMessages (SrcFolder : STRING);
    VAR DstFolder,S : STRING;
        Err       : INTEGER;
        P         : INTEGER;
        MailMessage,
        MailFlags,
        MailTime  : STRING;
        NumMsgs,
        NumErrs,
        Max       : INTEGER;
        F         : FILE;
    BEGIN
      IF NOT FolderCopyCheck (SrcFolder) THEN
      BEGIN
        Log (log_message, 'Skipping '+SrcFolder);
        EXIT;
      END;
      IF Src.isPublicFolder (SrcFolder) THEN
      BEGIN
        Log (log_message, 'Skipping Public Folder '+SrcFolder);
        EXIT;
      END;
      IF Src.isOtherUsersFoler (SrcFolder) THEN
      BEGIN
        Log (log_message, 'Skipping other users Folder '+SrcFolder);
        EXIT;
      END;


      Write ('Getting List of messages in "'+SrcFolder+'" ');
      Err := Src.SelectMailbox (SrcFolder);

      // some servers dont support selecting a mailbox that is not
      // subscribed
      IF (Err <> 0) AND (SubscribeSrcFolder) THEN
      BEGIN
        Log (log_warning, 'SELECT '+SrcFolder+' failed, trying to SUBSCRIBE');
        Src.SubscribeMailbox (SrcFolder);
        Err := Src.SelectMailbox (SrcFolder);
      END;

      IF Err <> 0 THEN
      BEGIN
        Log (log_error, 'Failed (SELECT)- copy aborted for this folder');
        INC (TotalErrs);
        INC (NumUserErrs);
        INC (TotalFoldersNotCopieed);
        EXIT;
      END;
      WriteLn ('OK, ',Src.NumMessages,' Messages found');

      IF Src.NumMessages = 0 THEN
        IF NOT CreateEmptyFolders THEN
          IF NOT CopyFoldersOnly THEN
          BEGIN
            log (log_message, 'Folder '+SrcFolder+' contains no messages - skipping');
            EXIT;
          END;


      WriteLn ('Processing Folder ',SrcFolder);
      NumMsgs := 0;
      NumErrs := 0;

      IF NOT CopyAllMessagesToInbox THEN
      BEGIN
        DstFolder := Dst.NamespaceUser;
        IF DstRootFolder <> '' THEN          // AD 2003/12/14
        BEGIN
          IF DstFolder <> '' THEN
            DstFolder := DstFolder + Dst.PathSep;
          DstFolder := DstFolder + DstRootFolder;
        END;

        IF (SrcFolder <> Src.NamespaceUser) AND (SrcFolder <> 'INBOX') THEN  // AD 2001/10/09
        BEGIN
          S := Src.RemovePersonalNamespace (SrcFolder);
          P := Pos (Src.PathSep, S);
          WHILE (P > 0) DO
          BEGIN
            IF DstFolder <> '' THEN
              DstFolder := DstFolder + Dst.PathSep;
            DstFolder := DstFolder + Copy (S,1,P-1);
            Delete (S,1,P-1+Length(Src.PathSep));
            P := Pos (Src.PathSep, S);
          END;
          IF DstFolder <> '' THEN
            DstFolder := DstFolder + Dst.PathSep;
          DstFolder := DstFolder + S;
        END ELSE                                // AD 2001/10/03
        BEGIN
          // is root
          DstFolder := Dst.NamespaceUser;
          IF DstFolder = '' THEN
            DstFolder := 'INBOX';
        END;
      END ELSE
      BEGIN  // copy all messages to INBOX
        DstFolder := Dst.NamespaceUser;
        IF DstFolder = '' THEN
          DstFolder := 'INBOX';
      END;
      IF (Pos ('INBOX.INBOX.', DstFolder) = 1) THEN Delete (DstFolder, 1, 6);

      Err := Dst.SelectMailbox (DstFolder);
      IF Err <> 0 THEN
      BEGIN
        Write ('Creating folder "'+DstFolder+'" ');
        Err := Dst.CreateMailbox (DstFolder);
        IF Err <> 0 THEN
        BEGIN
          WriteLn ('FAILED');
          Log (log_error, 'Create Destination Folder ("'+DstFolder+'") failed - copy for this folder aborted');
          INC (TotalFolderCreateErrs);
          INC (NumUserErrs);
          INC (TotalFoldersNotCopieed);
          EXIT;
        END ELSE
        BEGIN
          WriteLn ('OK');
          INC (TotalFoldersCreated);
        END;
        Err := Dst.SelectMailbox (DstFolder);
        IF Err <> 0 THEN
        BEGIN
          Log (log_error, 'Select after Create Destination Folder ("'+DstFolder+'") failed - copy for this folder aborted');
          INC (TotalErrs);
          INC (NumUserErrs);
          INC (TotalFoldersNotCopieed);
          EXIT;
        END;
      END;

      IF SubscribeFolder THEN
      BEGIN
        Err := Dst.SubscribeMailbox (DstFolder);
        IF Err <> 0 THEN
        BEGIN
          Log (log_warning, 'Subscribe Destination Folder ("'+DstFolder+'") failed - will continue');
          INC (TotalErrs);
          INC (NumUserErrs);
        END ELSE
          Log (log_message, 'Subscribed Folder "'+DstFolder+'"');
      END;

      Max := Src.NumMessages;
      IF Max > 0 THEN
      BEGIN
        IF OnlyOneMessage THEN Max := 1;
        IF CopyFoldersOnly THEN Max := 0;
      END;
      IF Max > 0 THEN
      BEGIN
        FOR P := 1 TO Max DO
        BEGIN
          Write (#13,'reading message ',P);
          MailFlags    := Src.MessageFlags (P);
          MailTime     := Src.MessageDate (P);
          IF MailTime = '' THEN
            MailTime := Src.RFC822MessageDate (P);  // for Lotus notes, sometimes notes returns invalid dates
          MailMessage  := Src.MessageBody (P);
          IF MailMessage <> '' THEN
          BEGIN
            Write (#13,'WRITING');
            {Retry := 0;
            REPEAT
              Err := Dst.MessageSave (DstFolder,MailMessage,MailFlags,MailTime);
              INC (Retry);
              IF (Err <> WSAECONNABORTED) AND (Err <> WSAECONNRESET) THEN
                Retry := 10;   // the mercury v3.30 beta sometimes closes the connection !???@@!!
            UNTIL (Err = 0) OR (Retry > 2);
            -- this was a test, it seems to be an error in mercury }
            RemoveUnsupportedFlags (MailFlags);
            Err := Dst.MessageSave (DstFolder,MailMessage,MailFlags,MailTime);
            IF Err <> 0 THEN
            BEGIN
              Write (#13);
              Log (log_error,'Error saving message '+IntToStr(P)+', continuing copy, Server returned "'+Dst.LastResult+'"');
              INC (NumErrs);
              INC (NumUserErrs);
              INC (TotalErrs);

              // Save failed message
              Assign (F,'FailedMail.dat');
              ReWrite (F,1);
              BlockWrite (F,MailMessage[1], Length (MailMessage));
              Close (F);
            END ELSE
            BEGIN
              INC (NumMsgs);
              INC (NumUserMessages);
              INC (TotalNumMsgs);
            END;
          END ELSE
          BEGIN
            Write (#13);
            Log (Log_warning,'WARNING: Empty body for message '+IntToStr(P)+' received from server, skipping message');
            INC (NumErrs);
            INC (NumUserErrs);
            INC (TotalErrs);
          END;
        END;
        Write (#13);
        if logfile.log_to_file then
          Log (log_status,IntToStr(NumMsgs)+' Messages copied, '+IntToStr(NumErrs)+' Errors ('+SrcFolder+' -> '+DstFolder+')')
        else
          Log (log_status,IntToStr(NumMsgs)+' Messages copied, '+IntToStr(NumErrs)+' Errors');
      END ELSE
        IF NOT CopyFoldersOnly THEN
          Log (log_message, 'Folder contains no messages - skipping');
    END;


BEGIN
  Assign (T,CfgFileK);
  Reset (T);
  {$IFDEF Unix}
  IF IOResult <> 0 THEN
  BEGIN  // check lower case
    Assign (T,CfgFile2K);
    Reset (T);
  END;
  {$ENDIF}

  IF IOResult <> 0 THEN
  BEGIN
    {$IFDEF Unix}
    WriteLn (CfgFileK+' or '+CfgFile2K+' not found');
    {$else}
    WriteLn (CfgFileK+' not found');
    {$ENDIF}
    EXIT;
  END;
  SrcServer := '';
  DstServer := '';
  SrcPort := 0;
  DstPort := 0;
  Src := NIL;
  Dst := NIL;
  DebugCommandsSrc := FALSE;
  DebugCommandsDst := FALSE;
  TotalNumMsgs  := 0;
  TotalErrs     := 0;
  REPEAT
    Readln (T, Line);
    IF (Copy (Line,1,1) = '#') OR (Copy (Line,1,1) = ';') THEN
      cmd := ''
    ELSE
      Cmd := lowerCase (CfgNextWord (Line));
    IF (Cmd = '') OR (cmd = '#') OR (cmd = ';') THEN
    BEGIN
    END ELSE
    IF Cmd = 'skipfolder' THEN
      SrcSkipFolders := SrcSkipFolders + CfgNextWord (Line) + ','
    ELSE
    IF Cmd = 'skipmatch' THEN
    BEGIN
      IF SrcMatchFolders = nil then
        SrcMatchFolders := TStringList.Create;
      SrcMatchFolders.Add (CfgNextWord (Line));
    END ELSE
    IF Cmd = 'copyfolder' THEN
      SrcThisFoldersOnly := SrcThisFoldersOnly + CfgNextWord (Line) + ','
    ELSE
    IF Cmd = 'createemptyfolders' THEN CreateEmptyFolders := TRUE
    ELSE
    IF Cmd = 'debugsrc' THEN DebugCommandsSrc := TRUE
    ELSE
    IF Cmd = 'debugdst' THEN DebugCommandsDst := TRUE
    ELSE
    IF Cmd = 'sourceserver' THEN SrcServer := CfgNextWord (Line)
    ELSE
    IF Cmd = 'destserver' THEN DstServer := CfgNextWord (Line)
    ELSE
    IF Cmd = 'sourceport' THEN SrcPort := Word (StrToInt (CfgNextWord (Line)))
    ELSE
    IF Cmd = 'destport' THEN DstPort := Word (StrToInt (CfgNextWord (Line)))
    ELSE
    IF Cmd = 'allowflags' THEN AllowFlags := CfgNextWord (Line)
    ELSE
    IF Cmd = 'denyflags' THEN DenyFlags := CfgNextWord (Line)
    ELSE
    IF Cmd = 'copyfoldersonly' THEN CopyFoldersOnly := TRUE
    ELSE
    IF Cmd = 'subscribefolder' THEN SubscribeFolder := TRUE
    ELSE
    IF Cmd = 'subscribesrcfolder' THEN SubscribeFolder := TRUE
    ELSE
    IF Cmd = 'dstrootfolder' THEN DstRootFolder := CfgNextWord (Line)
    ELSE
    IF Cmd = 'converttimezone' THEN
    BEGIN
      S := CfgNextWord(Line);
      S2 := CfgNextWord(Line);
      AddTimeZoneConversionEntry (S,S2);
    END ELSE
    IF Cmd = 'copy' THEN
    BEGIN
      IF Src = NIL THEN
      BEGIN
        IF SrcServer = '' THEN
        BEGIN
          log (log_fatal, 'SourceServer missing in configfile');
          EXIT;
        END;
        Src := ImapServer.Create;
        Err := Src.Connect (SrcServer, SrcPort);
        IF Err <> 0 THEN
        BEGIN
          log (log_fatal, 'Unable to connect to source imap-server "'+SrcServer+':'+IntToStr(SrcPort)+'"');
          EXIT;
        END;
      END;
      IF Dst = NIL THEN
      BEGIN
        IF DstServer = '' THEN
        BEGIN
          log (log_fatal, 'DestServer missing in configfile');
          EXIT;
        END;
        Dst := ImapServer.Create;
        Err := Dst.Connect (DstServer, DstPort);
        IF Err <> 0 THEN
        BEGIN
          log (log_fatal, 'Unable to connect to destination imap-server "'+DstServer+':'+IntToStr(DstPort)+'"');
          EXIT;
        END;
      END;
      SrcUser   := CfgNextWord (Line);
      SrcPasswd := CfgNextWord (Line);
      DstUser   := CfgNextWord (Line);
      DstPasswd := CfgNextWord (Line);

      NumUserMessages := 0;
      NumUserErrs := 0;

      IF DstUser = '*' THEN DstUser := SrcUser;
      IF DstPasswd = '*' THEN DstPasswd := SrcPasswd;

      log (log_file,'---- Beginning copy of '+SrcUser);

      Write ('Login on sourceserver as '+SrcUser+' ');
      Err := Src.Login (SrcUser, SrcPasswd);
      IF Err = 0 THEN
      BEGIN
        WriteLn ('OK');
        Write ('Login on destinationserver as '+DstUser+' ');
        Err := Dst.Login (DstUser, DstPasswd);
        IF Err = 0 THEN
        BEGIN
          WriteLn ('OK');
          INC (TotalUsers);

          Src.DebugCommands := DebugCommandsSrc;
          Dst.DebugCommands := DebugCommandsDst;

          IF InfoOnly THEN
          BEGIN
            WriteLn ('Sourceserver:');
            WriteLn ('=============');
            WriteLn ('Server-Info          : ',Src.ServerInfo);
            WriteLn ('Capabilities         : ',Src.Capabilities);
            WriteLn ('Personal Namespace   : ',Src.NamespaceUser);
            WriteLn ('Folder sperator      : ',Src.PathSep);
            WriteLn ('other Users Namespace: ', Src.NamespaceOtherUser);
            WriteLn ('Public Namespace     : ', Src.NamespacePublicFolders);
            IF SrcThisFoldersOnly <> '' THEN
              S := SrcThisFoldersOnly ELSE S := 'ALL';
            WriteLn ('Folders to copy      : ',S);
            IF SrcSkipFolders <> '' THEN
              S := SrcSkipFolders ELSE S := 'NONE';
            WriteLn ('Skip this folders    : ',S);

            WriteLn;
            WriteLn ('Destinationserver:');
            WriteLn ('==================');
            WriteLn ('Server-Info          : ',Dst.ServerInfo);
            WriteLn ('Capabilities         : ',Dst.Capabilities);
            WriteLn ('Personal Namespace   : ',Dst.NamespaceUser);
            WriteLn ('Folder sperator      : ',Dst.PathSep);
            WriteLn ('other Users Namespace: ', Dst.NamespaceOtherUser);
            WriteLn ('Public Namespace     : ', Dst.NamespacePublicFolders);
            Src.Free;
            Dst.Free;
            HALT;
          END ELSE
          BEGIN
            Write ('Getting folderlist on sourceserver ');
            Err := Src.GetMailboxList;
            IF Err = 0 THEN
            BEGIN
              WriteLn ('OK, found ',Src.MailboxList.Count,' folder');
              IF DebugCommandsSrc THEN
              BEGIN
                FOR I := 0 TO Src.MailboxList.Count-1 DO
                  Writeln (Src.MailboxList[I]);
                Writeln;
              END;
              IF Src.MailboxList.Count > 0 THEN
              BEGIN
                IF DstRootFolder <> '' THEN
                BEGIN
                  S := Dst.NamespaceUser;
                  IF S <> '' THEN
                    S := S + Dst.PathSep;
                  S := S + DstRootFolder;
                  Writeln ('Creating Rootfolder '+S);
                  Err := Dst.SelectMailbox (S);
                  IF Err <> 0 THEN
                  BEGIN
                    Write ('Creating rootfolder "'+S+'" ');
                    Err := Dst.CreateMailbox (S);
                    IF Err <> 0 THEN
                    BEGIN
                      WriteLn ('FAILED');
                      log (log_error, 'Create Destination Folder ("'+S+'") failed - copy aborted');
                      INC (TotalFolderCreateErrs);
                      INC (NumUserErrs);
                      EXIT;
                    END ELSE
                      Writeln ('Ok');
                  END;
                  IF SubscribeFolder THEN
                  BEGIN
                    Err := Dst.SubscribeMailbox (S);
                    IF Err <> 0 THEN
                    BEGIN
                      log (log_warning,'Subscribe Destination Folder ("'+S+'") failed - will continue');
                      INC (TotalErrs);
                    END ELSE
                      log (log_message, 'Subscribed Folder "'+S+'"');
                  END;
                END;

                IF NOT TestMode THEN
                  FOR I := 0 TO Src.MailboxList.Count-1 DO
                    CopyAllMessages (Src.MailboxList[I]);
              END ELSE
                Log (log_error,'no folders found on source ?? at least INBOX has to be there !');
            END ELSE
            BEGIN
              WriteLn ('FAILED - copy aborted for this user');
              log (log_error,'Getting Folderlist for User '+SrcUser+' failed');
            END;
          END;

          Src.Logout;
          Dst.Logout;

          if NumUserErrs > 0 then
            log (log_error,'Total Errors for User "'+SrcUser+'": '+IntToStr(NumUserErrs));
          Log (log_file,'---- End of job for user "'+SrcUser+'", copied '+IntToStr(NumUserMessages)+' Message(s)');
        END ELSE
        BEGIN
          WriteLn ('FAILED !');
          log (log_fatal, 'Login on destinationserver as '+DstUser+' failed');
          Src.Logout;
        END;
        IF Src <> NIL THEN
        BEGIN
          Src.Free; Src := NIL;
        END;
        IF Dst <> NIL THEN
        BEGIN
          Dst.Free; Dst := NIL;
        END;

      END ELSE
      BEGIN
        WriteLn ('FAILED !');
        log (log_fatal,'Login on sourceserver as '+SrcUser+' failed');
      END;
    END ELSE
    BEGIN
      log (log_fatal,'Unkown command ('+cmd+') found in configfile');
      EXIT;
    END;

  UNTIL EOF (T);
  WriteLn;
  log (log_status,IntToStr(TotalUsers)+' User processed, '+IntToStr(TotalNumMsgs)+' Messages copied, '+IntToStr(TotalErrs)+' Error(s)');
  log (log_status,IntToStr(TotalFoldersCreated)+' Folder(s) created, '+IntToStr(TotalFolderCreateErrs)+' Folder create errors, '+IntToStr(TotalFoldersNotCopieed)+' Folder not copied');
END;

BEGIN
  AllowFlagsList := NIL;
END.
