
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
    previousBalance?: number;
    currentRent?: number;
    increaseAnchorDate?: string;
    pendingSurcharge?: number;
    interestAmount?: number;
    lastGeneratedMonthYear?: string;
    nextPaymentDay?: Date | string | null;
    nextIncreaseDay?: Date | string | null;
}

