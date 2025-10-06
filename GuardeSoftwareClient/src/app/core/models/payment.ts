
export interface Payment{
    id: number;
    paymentMethodId: number;
    clientId: number;
    amount: number;
    paymentDate: Date;
    clientName?: string;
    paymentIdentifier?: string;
}

