/*---------------------------------------------------------------------------------------------
 *  Copyright (c) Microsoft Corporation. All rights reserved.
 *  Licensed under the Microsoft License. See License.txt in the project root for license information.
 *--------------------------------------------------------------------------------------------*/

namespace CustomerLicenseAgreement.Core.Primitives.Data
{
    using System.Collections.Generic;
    using GitOps.Primitives.Abstractions;

    /// <summary>
    /// Use this to define the primitive.
    /// The primitive is the unit of computation that this app will act on.
    /// It can be thought of as a configuration for the app, that will be
    /// checked in by the user in source code as a policy configuration.
    ///
    /// The app can define any kind of model here and then implement a
    /// corresponding behavior of this primitive.
    /// </summary>
    public sealed class CLAPrimitive : IPrimitive
    {
        public string ClaContent { get; set; }

        public MinimalChangeRequired MinimalChangeRequired { get; set; }

        public IEnumerable<string> SkipUsers { get; set; }

        public IEnumerable<string> SkipOrgs { get; set; }
    }
}
