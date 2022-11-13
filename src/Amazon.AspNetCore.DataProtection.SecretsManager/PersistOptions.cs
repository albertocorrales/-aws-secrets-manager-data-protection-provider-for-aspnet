namespace Amazon.AspNetCore.DataProtection.SecretsManager
{
    public class PersistOptions
    {
        /// <summary>
        /// The KMS Key ID that you want to use to encrypt a parameter when you choose the SecureString data type. If you 
        /// don't specify a key ID, the system uses the default key associated with your AWS account.
        /// </summary>
        public string KMSKeyId { get; set; } = null;

        /// <summary>
        /// Optional region to replicate the secret
        /// </summary>
        public string ReplicationRegion { get; set; } = null;

        /// <summary>
        /// KMS Key ID to use in the replication region. If you 
        /// don't specify a key ID, the system uses the default key associated with your AWS account.
        /// </summary>
        public string ReplicaRegionKMSKeyId { get; set; } = null;
    }
}
