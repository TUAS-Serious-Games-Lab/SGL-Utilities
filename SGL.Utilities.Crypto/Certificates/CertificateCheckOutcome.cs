namespace SGL.Utilities.Crypto.Certificates {
	public enum CertificateCheckOutcome {
		Valid,
		OutOfValidityPeriod,
		InvalidSignature,
		OtherError
	}
}
