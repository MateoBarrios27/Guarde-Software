import { LockerClientDetailDTO } from "../locker/LockerClientDetailDTO";


export interface ClientDetailDTO{

    id: number;
    paymentIdentifier: number;
    name: string;
    lastName: string;
    city: string;
    state: string;
    cuit: string;
    dni: string;
    registrationDate: Date;

    //contact information
    email: string;
    phone: string;
    address: string;

    //payment and rental info
    ivaCondition: string;
    preferredPaymentMethod: string;
    increasePercentage: number;
    increaseFrequency: number;
    nextIncreaseDay: Date;
    nextPaymentDay: Date;
    balance: number;
    paymentStatus: string;
    rentAmount: number;

    //locker info
    lockersList?: LockerClientDetailDTO[];
    contractedM3: number;
    notes: number;
}
