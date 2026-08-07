import { Injectable } from '@angular/core';
import * as signalR from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../../environments/environments';

export interface PaymentPresenceUser {
  userId: string;
  userName: string;
  displayName: string;
  joinedAtUtc: string;
}

export interface PaymentPresenceChanged {
  clientId: number;
  viewers: PaymentPresenceUser[];
}

export interface PaymentPresenceJoinResult {
  clientId: number;
  stateToken: string;
  viewers: PaymentPresenceUser[];
}

export interface PaymentCompletedNotice {
  clientId: number;
  payerName: string;
  payerUserName: string;
  amount: number;
  paymentDate: string;
  recordedAtUtc: string;
  concept?: string | null;
}

@Injectable({ providedIn: 'root' })
export class PaymentPresenceService {
  private hubConnection?: signalR.HubConnection;
  private startPromise?: Promise<void>;
  private readonly roomReferences = new Map<number, number>();
  private readonly snapshots = new Map<number, PaymentPresenceJoinResult>();

  private readonly presenceChangedSource = new Subject<PaymentPresenceChanged>();
  private readonly paymentCompletedSource = new Subject<PaymentCompletedNotice>();
  private readonly connectionIssueSource = new Subject<boolean>();

  readonly onPresenceChanged$ = this.presenceChangedSource.asObservable();
  readonly onPaymentCompleted$ = this.paymentCompletedSource.asObservable();
  readonly onConnectionIssue$ = this.connectionIssueSource.asObservable();

  async joinClientRoom(clientId: number): Promise<PaymentPresenceJoinResult | null> {
    if (!clientId) {
      return null;
    }

    await this.startConnection();

    const references = this.roomReferences.get(clientId) ?? 0;
    if (references > 0) {
      this.roomReferences.set(clientId, references + 1);
      return this.snapshots.get(clientId) ?? null;
    }

    try {
      const snapshot = await this.hubConnection!.invoke<PaymentPresenceJoinResult>('JoinClientRoom', clientId);
      this.roomReferences.set(clientId, 1);
      this.publishSnapshot(snapshot);
      return snapshot;
    } catch (error) {
      this.connectionIssueSource.next(true);
      throw error;
    }
  }

  async leaveClientRoom(clientId: number): Promise<void> {
    if (!clientId) {
      return;
    }

    const references = this.roomReferences.get(clientId) ?? 0;
    if (references > 1) {
      this.roomReferences.set(clientId, references - 1);
      return;
    }

    this.roomReferences.delete(clientId);
    this.snapshots.delete(clientId);

    if (!this.hubConnection || this.hubConnection.state === signalR.HubConnectionState.Disconnected) {
      return;
    }

    try {
      await this.hubConnection.invoke('LeaveClientRoom', clientId);
    } catch {
      this.connectionIssueSource.next(true);
    }
  }

  async refreshClientState(clientId: number): Promise<PaymentPresenceJoinResult | null> {
    if (!clientId) {
      return null;
    }

    try {
      await this.startConnection();
      const snapshot = await this.hubConnection!.invoke<PaymentPresenceJoinResult>('RefreshClientState', clientId);
      this.publishSnapshot(snapshot);
      return snapshot;
    } catch {
      this.connectionIssueSource.next(true);
      return null;
    }
  }

  private async startConnection(): Promise<void> {
    if (this.hubConnection?.state === signalR.HubConnectionState.Connected) {
      return;
    }

    if (this.startPromise) {
      return this.startPromise;
    }

    if (!this.hubConnection) {
      const token = localStorage.getItem('authToken');
      const hubUrl = environment.apiUrl.replace('/api', '') + '/hubs/payment-presence';

      this.hubConnection = new signalR.HubConnectionBuilder()
        .withUrl(hubUrl, {
          accessTokenFactory: () => token ?? '',
          skipNegotiation: false,
          transport: signalR.HttpTransportType.WebSockets
        })
        .withAutomaticReconnect([0, 2000, 5000, 10000])
        .configureLogging(signalR.LogLevel.Warning)
        .build();

      this.hubConnection.on('PresenceChanged', (event: PaymentPresenceChanged) => {
        this.presenceChangedSource.next(event);
      });
      this.hubConnection.on('PaymentCompleted', (event: PaymentCompletedNotice) => {
        this.paymentCompletedSource.next(event);
      });
      this.hubConnection.onreconnecting(() => this.connectionIssueSource.next(true));
      this.hubConnection.onreconnected(async () => {
        this.connectionIssueSource.next(false);
        for (const clientId of this.roomReferences.keys()) {
          try {
            const snapshot = await this.hubConnection!.invoke<PaymentPresenceJoinResult>('JoinClientRoom', clientId);
            this.publishSnapshot(snapshot);
          } catch {
            this.connectionIssueSource.next(true);
          }
        }
      });
      this.hubConnection.onclose(() => this.connectionIssueSource.next(true));
    }

    this.startPromise = this.hubConnection.start()
      .then(() => this.connectionIssueSource.next(false))
      .catch(error => {
        this.connectionIssueSource.next(true);
        throw error;
      })
      .finally(() => {
        this.startPromise = undefined;
      });

    return this.startPromise;
  }

  private publishSnapshot(snapshot: PaymentPresenceJoinResult): void {
    this.snapshots.set(snapshot.clientId, snapshot);
    this.presenceChangedSource.next({ clientId: snapshot.clientId, viewers: snapshot.viewers ?? [] });
  }
}
