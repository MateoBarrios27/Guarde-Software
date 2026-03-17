
export interface Client{
    id: number;
    paymentIdentifier: number;
    fullName: string;
    registrationDate: Date;
    notes: string;
    dni: string;
    cuit: string;
    preferredPaymentMethod: number;
    ivaCondition: string;
    balance?: number;
}

