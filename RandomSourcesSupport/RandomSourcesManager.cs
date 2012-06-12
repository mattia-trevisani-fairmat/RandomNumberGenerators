﻿/* Copyright (C) 2012 Fairmat SRL (info@fairmat.com, http://www.fairmat.com/)
 * Author(s): Francesco Biondi (francesco.biondi@fairmat.com)
 *
 * This program is free software: you can redistribute it and/or modify
 * it under the terms of the GNU Lesser General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program.  If not, see <http://www.gnu.org/licenses/>.
 */

using System;
using System.Collections.Generic;
using DVPLI;
using Mono.Addins;

namespace RandomSourcesSupport
{
    /// <summary>
    /// Implements a random numbers generator that has the possibility to
    /// use a custom random source in order to get the numbers.
    /// </summary>
    [Extension("/Fairmat/RandomNumbersGenerator")]
    public unsafe class RandomSourceManager : IRandomNumbersGenerator
    {
        #region Fields
        /// <summary>
        /// The object responsible for managing the number generation and storing/loading the
        /// data generated.
        /// </summary>
        private IRandomSource randomSource;

        /// <summary>
        /// True if a valid value is stored in the bmSave field, false otherwise.
        /// </summary>
        private bool boxMullerState;

        /// <summary>
        /// Contains the second value generated by the last Box-Muller transformation call.
        /// </summary>
        private double boxMullerSave;

        /// <summary>
        /// The list of restore points.
        /// </summary>
        private List<IRandomGeneratorRestorePoint> restorePoints = new List<IRandomGeneratorRestorePoint>();

        /// <summary>
        /// True if the random number generator has been initialized, false otherwise.
        /// </summary>
        private bool initialized;
        #endregion // Fields

        #region Constructors
        /// <summary>
        /// Initializes the random numbers generator.
        /// </summary>
        public RandomSourceManager()
        {
            RandomSourceSettings settings = UserSettings.GetSettings(typeof(RandomSourceSettings)) as RandomSourceSettings;
            if (settings != null)
            {
                // Try reading the random source specified in the settings
                IRandomSource selectedRandomSource = null;
                foreach (TypeExtensionNode node in AddinManager.GetExtensionNodes("/RandomSourcesSupport/RandomSource"))
                {
                    IRandomSource randomSource = node.CreateInstance() as IRandomSource;
                    string randomSourceDescription;

                    if (randomSource is IDescription)
                        randomSourceDescription = ((IDescription)randomSource).Description;
                    else
                        randomSourceDescription = string.Empty;

                    if (randomSourceDescription == settings.RngRandomSource)
                    {
                        selectedRandomSource = randomSource;
                        break;
                    }
                }

                if (selectedRandomSource == null)
                {
                    // If the random source is still null use the default one
                    TypeExtensionNode node = ExtensionsDefault.GetDefault("/RandomSourcesSupport/RandomSource");
                    if (node != null)
                        selectedRandomSource = node.CreateInstance() as IRandomSource;
                }

                // Set the random source and initialize it
                this.randomSource = selectedRandomSource;
            }
        }
        #endregion // Constructors

        #region IRandomNumbersGenerator implementation
        /// <summary>
        /// Gets the random variables transformation object (not implemented).
        /// </summary>
        public IRandomVariablesGenerator Transformations
        {
            get
            {
                return null;
            }
        }

        /// <summary>
        /// Gets the implementation information about this random number generator.
        /// </summary>
        public ImplementationInfo ImplementationInfo
        {
            get
            {
                return new ImplementationInfo();
            }
        }

        /// <summary>
        /// Initializes the random numbers generator.
        /// </summary>
        public void InizializeNonRepeatable()
        {
            this.randomSource.InitializeNonRepeatable();
            this.boxMullerState = false;
            this.initialized = true;
        }

        /// <summary>
        /// Initializes the random numbers generator in order to get a repeatable sequence.
        /// </summary>
        /// <param name="p_Seed">The sequence Id.</param>
        public void InizializeRepeatable(int p_Seed)
        {
            this.randomSource.InitializeRepeatable(p_Seed);
            this.boxMullerState = false;
            this.initialized = true;
        }

        /// <summary>
        /// Restores the last saved state.
        /// </summary>
        public void Restore()
        {
            if (this.restorePoints.Count == 0)
                return;

            this.randomSource.LoadState(this.restorePoints[this.restorePoints.Count - 1]);
            this.restorePoints.RemoveAt(this.restorePoints.Count - 1);
        }

        /// <summary>
        /// Restores the number generator to the specified state.
        /// </summary>
        /// <param name="restorePoint">The restore point.</param>
        public void RestoreState(IRandomGeneratorRestorePoint restorePoint)
        {
            this.randomSource.LoadState(restorePoint);
        }

        /// <summary>
        /// Saves the current state of the generator.
        /// </summary>
        public void Save()
        {
            this.restorePoints.Add(this.randomSource.GetState());
        }

        /// <summary>
        /// Saves the state of the number generator and returns it.
        /// </summary>
        /// <returns>The restore point representing the status.</returns>
        public IRandomGeneratorRestorePoint SaveState()
        {
            return this.randomSource.GetState();
        }

        /// <summary>
        /// Gets a randomly generated uniform value.
        /// </summary>
        /// <returns>A randomly generated uniform value.</returns>
        public double Uniform()
        {
            if (!this.initialized)
                InizializeNonRepeatable();

            return this.randomSource.Next();
        }

        /// <summary>
        /// Initializes the given area of doubles with randomly generated normal values.
        /// </summary>
        /// <param name="samples">The pointer representing the start of the area. </param>
        /// <param name="n">The length of the area.</param>
        public void Normal(double* samples, int n)
        {
            if (!this.initialized)
                InizializeNonRepeatable();

            for (int i = 0; i < n; i++)
            {
                samples[i] = BoxMuller();
            }
        }

        /// <summary>
        /// Gets a randomly generated normal value.
        /// </summary>
        /// <returns>A randomly generated normal value.</returns>
        public double Normal()
        {
            if (!this.initialized)
                InizializeNonRepeatable();

            return BoxMuller();
        }
        #endregion // IRandomNumbersGenerator implementation

        #region Utility methods
        /// <summary>
        /// Generates two normals starting from two random variates.
        /// <para>The first value is initially returned and when the method is called a
        /// second time the second value is returned.</para>
        /// </summary>
        /// <returns>The first normal number (or the second one if the first has already
        /// been returned) generated by the transformation.</returns>
        private double BoxMuller()
        {
            if (!this.boxMullerState)
            {
                double u1 = Uniform();
                double u2 = Uniform();

                double r = -2 * Math.Log(u1);
                double v = 2 * Math.PI * u2;
                double sqrtr = Math.Sqrt(r);

                this.boxMullerSave = sqrtr * Math.Sin(v);
                this.boxMullerState = true;
                return sqrtr * Math.Cos(v);
            }
            else
            {
                this.boxMullerState = false;
                return this.boxMullerSave;
            }
        }
        #endregion // Utility methods
    }
}
