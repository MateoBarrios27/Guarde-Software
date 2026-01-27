
export interface Client{
    id: number;
    paymentIdentifier: number;
    firstName: string;
    lastName: string;
    registrationDate: Date;
    notes: string;
    dni: string;
    cuit: string;
    preferredPaymentMethod: number;
    ivaCondition: string;
    balance?: number;
}

